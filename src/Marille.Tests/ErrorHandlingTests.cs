using Marille.Tests.Workers;

namespace Marille.Tests;

public class ErrorHandlingTests : BaseTimeoutTest, IDisposable {

	readonly Hub _hub;
	readonly ErrorWorker<WorkQueuesEvent> _errorWorker;
	readonly TaskCompletionSource<bool> _errorWorkerTcs;
	TopicConfiguration _configuration;

	public ErrorHandlingTests () : base (milliseconds: 10000)
	{
		_hub = new();
		_errorWorkerTcs = new();
		_errorWorker = new(_errorWorkerTcs);
		_configuration = new();
	}

	public void Dispose ()
	{
		_hub.Dispose ();
		_errorWorker.Dispose ();
	}

	[Fact]
	public async Task ErrorWorkerConsumeAsync ()
	{
		// create a new topic, post 3 events and consume them, one of them will throw an exception
		// and we should be able to handle it appropriately
		using var cts = GetCancellationToken ();
		var topic = nameof(ErrorLambdaConsumeAsync);
		var workerTcs = new TaskCompletionSource<bool> ();
		var worker = new FastWorker ("worker1", workerTcs);
		await _hub.CreateAsync (topic, _configuration, _errorWorker, worker);
		// we will post 3 events, one of them will throw an exception
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("1"), cts.Token);
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("2", true), cts.Token);
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("3"), cts.Token);
		// await for the error worker to consume the events
		await _errorWorkerTcs.Task;
		Assert.Equal (1, _errorWorker.ConsumedCount);
		// assert the id of the message and the exception
		Assert.Equal ("2", _errorWorker.ConsumedMessages [0].Message.Id);
		Assert.Equal (typeof(InvalidOperationException),
			_errorWorker.ConsumedMessages [0].Exception.GetType ());
	}

	[Fact]
	public async Task ErrorLambdaConsumeAsync ()
	{
		// use the lambda notation to consume the events and assert that the error worker is called correctly
		using var cts = GetCancellationToken ();
		var topic = nameof(ErrorLambdaConsumeAsync);
		int consumedCount = 0;
		var messageId = string.Empty;
		var tcs = new TaskCompletionSource<bool> ();
		Exception? exception = null;
		Func<WorkQueuesEvent, Exception, CancellationToken, Task> errorAction = (message, exceptionIn, _) => {
			Interlocked.Increment (ref consumedCount);
			messageId = message.Id;
			exception = exceptionIn;
			tcs.TrySetResult (true);
			return Task.FromResult (Task.CompletedTask);
		};
		Func<WorkQueuesEvent, CancellationToken, Task> workerAction = (message, token) => {
			if (message.IsError) {
				throw new InvalidOperationException ($"Message with Id {message.Id} is an error");
			}

			return Task.CompletedTask;
		};
		await _hub.CreateAsync (topic, _configuration, errorAction, workerAction);
		// we will post 3 events, one of them will throw an exception
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("1"), cts.Token);
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("2", true), cts.Token);
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("3"), cts.Token);
		// await for the error worker to consume the events
		await tcs.Task;
		Assert.Equal (1, consumedCount);
		// assert the id of the message and the exception
		Assert.Equal ("2", messageId);
		Assert.Equal (typeof(InvalidOperationException),
			exception!.GetType ());
	}

	[Fact]
	public async Task FaultyErrorWorker ()
	{
		// make sure that we do know how to deal with a situation in which we have a faulty error worker that
		// throws an exception
		using var cts = GetCancellationToken ();
		var topic = nameof(ErrorLambdaConsumeAsync);
		int consumedCount = 0;
		var messageId = string.Empty;
		var tcs = new TaskCompletionSource<bool> ();
		Func<WorkQueuesEvent, Exception, CancellationToken, Task> errorAction = (_, _, _) => {
			// Terrible idea, but users make mistakes
			throw new InvalidOperationException ($"Bad error worker");
		};
		Func<WorkQueuesEvent, CancellationToken, Task> workerAction = (message, _) => {
			if (message.IsError) {
				messageId = message.Id;
				throw new InvalidOperationException ($"Error message with id {message.Id}");
			}

			if (Interlocked.Increment (ref consumedCount) == 2)
				tcs.TrySetResult (true);
			return Task.CompletedTask;
		};
		await _hub.CreateAsync (topic, _configuration, errorAction, workerAction);
		// we will post 3 events, one of them will throw an exception, if we did not crash the consuming
		// thread, we we should be able to consume all the events
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("1"), cts.Token);
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("2", true), cts.Token);
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("3"), cts.Token);
		// await for the error worker to consume the events
		await tcs.Task.WaitAsync (cts.Token);
		Assert.Equal (2, consumedCount);
		// assert the id of the message and the exception
		Assert.Equal ("2", messageId);
	}

	[Fact]
	public async Task FaultyWorkerWithSuccessfulRetries ()
	{
		// create a message and throw an exception, make sure that we can handle the retries
		using var cts = GetCancellationToken ();
		var topic = nameof(FaultyWorkerWithSuccessfulRetries);
		int consumedCount = 0;
		var messageId = string.Empty;
		var tcs = new TaskCompletionSource<bool> ();
		var errorCount = 0;
		Func<WorkQueuesEvent, Exception, CancellationToken, Task> errorAction = (_, _, _) => {
			// Terrible idea, but users make mistakes
			Interlocked.Increment (ref errorCount);
			return Task.CompletedTask;
		};
		Func<WorkQueuesEvent, CancellationToken, Task> workerAction = (message, _) => {
			// throw while consuming the message until we have tried 2 times
			if (Interlocked.Increment (ref consumedCount) < 2)
				throw new InvalidOperationException ($"Error message with id {message.Id}");

			messageId = message.Id;
			tcs.TrySetResult (true);
			return Task.CompletedTask;
		};
		_configuration.MaxRetries = 4;
		await _hub.CreateAsync (topic, _configuration, errorAction, workerAction);
		// post a single event that will throw an exception, but we should be able to handle it in a retry
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("1"), cts.Token);
		// await for the error worker to consume the events
		await tcs.Task.WaitAsync (cts.Token);
		Assert.Equal (0, errorCount);
		Assert.Equal (2, consumedCount);
		// assert the id of the message and the exception
		Assert.Equal ("1", messageId);
	}

	[Fact]
	public async Task FaultyWorkerWithFailingRetries ()
	{
		// create a message an always fail	
		using var cts = GetCancellationToken ();
		var topic = nameof(FaultyWorkerWithSuccessfulRetries);
		int consumedCount = 0;
		var messageId = string.Empty;
		var tcs = new TaskCompletionSource<bool> ();
		var errorCount = 0;
		Func<WorkQueuesEvent, Exception, CancellationToken, Task> errorAction = (_, _, _) => {
			// Terrible idea, but users make mistakes
			Interlocked.Increment (ref errorCount);
			tcs.TrySetResult (true);
			return Task.CompletedTask;
		};
		Func<WorkQueuesEvent, CancellationToken, Task> workerAction = (message, _) => {
			// throw while consuming the message until we have tried 2 times
			Interlocked.Increment (ref consumedCount);
			messageId = message.Id;
			throw new InvalidOperationException ($"Error message with id {message.Id}");
		};
		_configuration.MaxRetries = 4;
		await _hub.CreateAsync (topic, _configuration, errorAction, workerAction);
		// post a single event that will throw an exception, but we should be able to handle it in a retry
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("1"), cts.Token);
		// await for the error worker to consume the events
		await tcs.Task.WaitAsync (cts.Token);
		Assert.Equal (1, errorCount);
		Assert.Equal (4, consumedCount);
		// assert the id of the message and the exception
		Assert.Equal ("1", messageId);
	}

}
