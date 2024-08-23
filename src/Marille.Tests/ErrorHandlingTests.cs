using Marille.Tests.Workers;

namespace Marille.Tests;

public class ErrorHandlingTests : IDisposable {

	readonly Hub _hub;
	readonly ErrorWorker<WorkQueuesEvent> _errorWorker;
	readonly TaskCompletionSource<bool> _errorWorkerTcs;
	TopicConfiguration _configuration;

	public ErrorHandlingTests ()
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
		var topic = nameof(ErrorLambdaConsumeAsync);
		var workerTcs = new TaskCompletionSource<bool> ();
		var worker = new FastWorker ("worker1", workerTcs);
		await _hub.CreateAsync (topic, _configuration, _errorWorker, worker);
		// we will post 3 events, one of them will throw an exception
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("1"));
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("2", true));
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("3"));
		// await for the error worker to consume the events
		await _errorWorkerTcs.Task;
		Assert.Equal (1, _errorWorker.ConsumedCount);
		// assert the id of the message and the exception
		Assert.Equal ("2", _errorWorker.ConsumedMessages[0].Message.Id);
		Assert.Equal (typeof(InvalidOperationException), 
			_errorWorker.ConsumedMessages[0].Exception.GetType ());
	}
	
	[Fact]
	public async Task ErrorLambdaConsumeAsync ()
	{
		// use the lambda notation to consume the events and assert that the error worker is called correctly
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
				throw new InvalidOperationException($"Message with Id {message.Id} is an error");
			}
			return Task.CompletedTask;
		};
		await _hub.CreateAsync (topic, _configuration, errorAction, workerAction);
		// we will post 3 events, one of them will throw an exception
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("1"));
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("2", true));
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("3"));
		// await for the error worker to consume the events
		await tcs.Task;
		Assert.Equal (1, consumedCount);
		// assert the id of the message and the exception
		Assert.Equal ("2", messageId);
		Assert.Equal (typeof(InvalidOperationException), 
			exception!.GetType ());
	}
}
