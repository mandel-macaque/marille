namespace Marille.Tests;

public class PublishSubscribeTests {
	readonly Hub _hub;
	readonly ErrorWorker<WorkQueuesEvent> _errorWorker;
	readonly TopicConfiguration _configuration;

	public PublishSubscribeTests ()
	{
		// use a simpler channel that we will use to receive the events when
		// a worker has completed its work
		_hub = new ();
		_errorWorker = new();
		_configuration = new();
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1000)]
	public async Task SingleProducerSeveralFastConsumers (int workerCount)
	{
		// create a collection of workers and ensure that all of them receive the single
		// message we send
		var workers = new List<(FastWorker Worker, TaskCompletionSource<bool> Tcs)> (workerCount);
		for (var index = 0; index < workerCount; index++) {
			var tcs = new TaskCompletionSource<bool> ();
			var worker = new FastWorker ($"worker{index + 1}", tcs);
			workers.Add ((worker, tcs));
		}
		var topic = "topic";
		await _hub.CreateAsync (topic, _configuration, _errorWorker, workers.Select (x => x.Worker));
		// publish a single message that will be received by all workers meaning we should wait for ALL their
		// task completion sources to be done
		await _hub.Publish (topic, new WorkQueuesEvent ("myID"));
		var result = await Task.WhenAll (workers.Select (x => x.Tcs.Task));
		Assert.Equal (workerCount, result.Length);
		// assert that all did indeed return true
		foreach (bool b in result) {
			// find a nicer way to match the worker with the bool
			Assert.True (b);
		}
		Assert.Equal (0, _errorWorker.ConsumedCount);
	}
	
	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1000)]
	public async Task SingleProducerSeveralSleepyConsumers (int workerCount)
	{
		// create a collection of workers and ensure that all of them receive the single
		// message we send
		var workers = new List<(SleepyWorker Worker, TaskCompletionSource<bool> Tcs)> (workerCount);
		for (var index = 0; index < workerCount; index++) {
			var tcs = new TaskCompletionSource<bool> ();
			var worker = new SleepyWorker($"worker{index + 1}", tcs);
			workers.Add ((worker, tcs));
		}
		var topic = "topic";
		await _hub.CreateAsync (topic, _configuration, _errorWorker, workers.Select (x => x.Worker));
		// publish a single message that will be received by all workers meaning we should wait for ALL their
		// task completion sources to be done
		await _hub.Publish (topic, new WorkQueuesEvent ("myID"));
		var result = await Task.WhenAll (workers.Select (x => x.Tcs.Task));
		Assert.Equal (workerCount, result.Length);
		// assert that all did indeed return true
		foreach (bool b in result) {
			// find a nicer way to match the worker with the bool
			Assert.True (b);
		}
	}
}
