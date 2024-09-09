using Marille.Tests.Workers;

namespace Marille.Tests;

// Set of tests that focus on the pattern in which a 
// several consumers register to a queue and the compete
// to consume an event.
public class WorkQueuesTests : BaseTimeoutTest, IDisposable {
	readonly Hub _hub;
	readonly ErrorWorker<WorkQueuesEvent> _errorWorker;
	readonly TaskCompletionSource<bool> _errorWorkerTcs;
	TopicConfiguration _configuration;
	readonly CancellationTokenSource _cancellationTokenSource;

	public WorkQueuesTests () : base(milliseconds: 10000)
	{
		// use a simpler channel that we will use to receive the events when
		// a worker has completed its work
		_hub = new ();
		_errorWorkerTcs = new();
		_errorWorker = new(_errorWorkerTcs);
		_configuration = new();
		_cancellationTokenSource = new();
		_cancellationTokenSource.CancelAfter (TimeSpan.FromSeconds (10));
	}
	
	public void Dispose ()
	{
		_hub.Dispose ();
		_errorWorker.Dispose ();
		_cancellationTokenSource.Dispose ();
	}

	// The simplest API usage, we register to an event for a topic with our worker
	// and the event should be consumed and added to the completed channel for use to verify
	[Theory]
	[InlineData(ChannelDeliveryMode.AtMostOnceAsync)]
	[InlineData(ChannelDeliveryMode.AtMostOnceSync)]
	public async Task SingleWorker (ChannelDeliveryMode deliveryMode)
	{
		using var cts = GetCancellationToken ();
		_configuration.Mode = deliveryMode;
		var topic = "topic";
		var tcs = new TaskCompletionSource<bool> ();
		var worker = new FastWorker ("myWorkerID", tcs);
		await _hub.CreateAsync (topic, _configuration, _errorWorker);
		await _hub.RegisterAsync (topic, worker);
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("myID"), cts.Token);
		Assert.True (await tcs.Task.WaitAsync (cts.Token));
		Assert.Equal (0, _errorWorker.ConsumedCount);
	}

	[Theory]
	[InlineData(ChannelDeliveryMode.AtMostOnceAsync)]
	[InlineData(ChannelDeliveryMode.AtMostOnceSync)]
	public async Task SingleAction (ChannelDeliveryMode deliveryMode)
	{
		using var cts = GetCancellationToken ();
		_configuration.Mode = deliveryMode;
		var topic = "topic";
		var tcs = new TaskCompletionSource<bool>();
		Func<WorkQueuesEvent, CancellationToken, Task> action = (_, _) =>
			Task.FromResult (tcs.TrySetResult(true));

		await _hub.CreateAsync (topic, _configuration, _errorWorker);
		Assert.True (await _hub.RegisterAsync (topic, action).WaitAsync (cts.Token));
		await _hub.PublishAsync (topic, new WorkQueuesEvent("myID"), cts.Token);
		Assert.True (await tcs.Task.WaitAsync (cts.Token));
		Assert.Equal (0, _errorWorker.ConsumedCount);
	}

	[Theory]
	[InlineData(ChannelDeliveryMode.AtMostOnceAsync)]
	[InlineData(ChannelDeliveryMode.AtMostOnceSync)]
	public async Task SeveralWorkers (ChannelDeliveryMode deliveryMode)
	{
		using var cts = GetCancellationToken ();
		_configuration.Mode = deliveryMode;
		string workerID = "myWorkerID";

		string topic1 = "topic1";
		var tcsWorker1 = new TaskCompletionSource<bool> ();
		await _hub.CreateAsync (topic1, _configuration, _errorWorker);

		string topic2 = "topic2";
		var tcsWorker2 = new TaskCompletionSource<bool> ();
		await _hub.CreateAsync (topic2, _configuration, _errorWorker);
		
		// ensure that each of the workers will receive data for the topic it is interested
		var worker1 = new FastWorker (workerID, tcsWorker1);
		Assert.True (await _hub.RegisterAsync (topic1, worker1).WaitAsync (cts.Token));

		Func<WorkQueuesEvent, CancellationToken, Task> worker2 = (_, _) 
			=> Task.FromResult (tcsWorker2.TrySetResult(true));
		
		Assert.True (await _hub.RegisterAsync (topic2, worker2).WaitAsync (cts.Token));

		// publish two both topics and ensure that each of the workers gets teh right data
		await _hub.PublishAsync (topic1, new WorkQueuesEvent ("1"), cts.Token);

		// we should only get the event in topic one, the second topic should be ignored
		Assert.True (await tcsWorker1.Task.WaitAsync (cts.Token));
		Assert.False (tcsWorker2.Task.IsCompleted);
		Assert.Equal (0, _errorWorker.ConsumedCount);
	}
	
	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1000)]
	public async Task SeveralWorkersSyncCall (int workerCount)
	{
		using var cts = GetCancellationToken ();
		// we create a collection of workers and will send a message to the topic, the workers will not
		// be able to process the rest of the messages until all the previous workers have completed their job
		var configuration = new TopicConfiguration { Mode = ChannelDeliveryMode.AtLeastOnceSync };
		var workers = new List<(SleepyWorker Worker, TaskCompletionSource<bool> Tcs)> (workerCount);
		for (var index = 0; index < workerCount; index++) {
			var tcs = new TaskCompletionSource<bool> ();
			var worker = new SleepyWorker($"worker{index + 1}", tcs);
			workers.Add ((worker, tcs));
		}

		var topic = nameof (SeveralWorkersSyncCall);
		await _hub.CreateAsync (topic, configuration, _errorWorker, workers.Select (x => x.Worker));
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("myID"), cts.Token);
		var result = await Task.WhenAll (workers.Select (x => x.Tcs.Task));
		Assert.Equal (workerCount, result.Length);
		// assert that all did indeed return true
		foreach (bool b in result) {
			// find a nicer way to match the worker with the bool
			Assert.True (b);
		}
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1000)]
	public async Task SeveralWorkersSyncCallOneThrows (int workerCount)
	{
		using var cts = GetCancellationToken ();
		var configuration = new TopicConfiguration { Mode = ChannelDeliveryMode.AtLeastOnceSync };
		var workers = new List<(SleepyWorker Worker, TaskCompletionSource<bool> Tcs)> (workerCount + 1);
		for (var index = 0; index < workerCount; index++) {
			var tcs = new TaskCompletionSource<bool> ();
			var worker = new SleepyWorker($"worker{index + 1}", tcs);
			workers.Add ((worker, tcs));
		}

		var topic = nameof (SeveralWorkersSyncCallOneThrows);
		// get all the workers and add an extra one that will throw an exception
		var allWorkers = workers.Select (x => x.Worker as IWorker<WorkQueuesEvent>).ToList ();
		var fastTcs = new TaskCompletionSource<bool> ();
		var fastWorker = new FastWorker ("fast1", fastTcs);
		allWorkers.Add (fastWorker);
		await _hub.CreateAsync (topic, configuration, _errorWorker, allWorkers);
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("myID", true), cts.Token);
		var result = await Task.WhenAll (workers.Select (x => x.Tcs.Task));
		Assert.Equal (workerCount, result.Length);
		// assert that all did indeed return true
		foreach (bool b in result) {
			// find a nicer way to match the worker with the bool
			Assert.True (b);
		}
		// wait until we have processed the error, else we are going to finish the test too early
		await _errorWorkerTcs.Task.WaitAsync (cts.Token);
		Assert.Equal (1, _errorWorker.ConsumedCount);
	}

}
