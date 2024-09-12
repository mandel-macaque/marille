using Marille.Tests.Workers;

namespace Marille.Tests;

public class PublishSubscribeTests : BaseTimeoutTest, IDisposable {
	readonly Hub _hub;
	readonly ErrorWorker<WorkQueuesEvent> _errorWorker;
	TopicConfiguration _configuration;

	public PublishSubscribeTests () : base(milliseconds:1000)
	{
		// use a simpler channel that we will use to receive the events when
		// a worker has completed its work
		_hub = new ();
		_errorWorker = new();
		_configuration = new() {
			Mode = ChannelDeliveryMode.AtLeastOnceSync
		};
	}
	
	public void Dispose ()
	{
		_hub.Dispose ();
		_errorWorker.Dispose ();
	}

	[Theory]
	[InlineData(1)]
	[InlineData(10)]
	[InlineData(100)]
	[InlineData(1000)]
	public async Task SingleProducerSeveralFastConsumersPublishAsync (int workerCount)
	{
		// create a collection of workers and ensure that all of them receive the single
		// message we send
		using var cts = GetCancellationToken ();
		var workers = new List<(FastWorker Worker, TaskCompletionSource<bool> Tcs)> (workerCount);
		for (var index = 0; index < workerCount; index++) {
			var tcs = new TaskCompletionSource<bool> ();
			var worker = new FastWorker ($"worker{index + 1}", tcs);
			workers.Add ((worker, tcs));
		}

		var topic = nameof (SingleProducerSeveralFastConsumersPublishAsync);
		await _hub.CreateAsync (topic, _configuration, _errorWorker, workers.Select (x => x.Worker));
		// publish a single message that will be received by all workers meaning we should wait for ALL their
		// task completion sources to be done
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("myID"), cts.Token);
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
	[InlineData (1)]
	[InlineData (10)]
	[InlineData (100)]
	[InlineData (1000)]
	public async Task SingleProducerSeveralFastConsumersTryPublish (int workerCount)
	{
		// create a collection of workers and ensure that all of them receive the single
		// message we send
		var workers = new List<(FastWorker Worker, TaskCompletionSource<bool> Tcs)> (workerCount);
		for (var index = 0; index < workerCount; index++) {
			var tcs = new TaskCompletionSource<bool> ();
			var worker = new FastWorker ($"worker{index + 1}", tcs);
			workers.Add ((worker, tcs));
		}

		var topic = nameof (SingleProducerSeveralFastConsumersTryPublish);
		await _hub.CreateAsync (topic, _configuration, _errorWorker, workers.Select (x => x.Worker));
		// publish a single message that will be received by all workers meaning we should wait for ALL their
		// task completion sources to be done
		Assert.True(_hub.TryPublish (topic, new WorkQueuesEvent ("myID")));
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
	public async Task SingleProducerSeveralSleepyConsumersPublishAsync (int workerCount)
	{
		using var cts = GetCancellationToken ();
		// create a collection of workers and ensure that all of them receive the single
		// message we send
		var workers = new List<(SleepyWorker Worker, TaskCompletionSource<bool> Tcs)> (workerCount);
		for (var index = 0; index < workerCount; index++) {
			var tcs = new TaskCompletionSource<bool> ();
			var worker = new SleepyWorker($"worker{index + 1}", tcs);
			workers.Add ((worker, tcs));
		}

		var topic = nameof (SingleProducerSeveralSleepyConsumersPublishAsync);
		await _hub.CreateAsync (topic, _configuration, _errorWorker, workers.Select (x => x.Worker));
		// publish a single message that will be received by all workers meaning we should wait for ALL their
		// task completion sources to be done
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
	[InlineData (1)]
	[InlineData (10)]
	[InlineData (100)]
	[InlineData (1000)]
	public async Task SingleProducerSeveralSleepyConsumersTryPublish (int workerCount)
	{
		// create a collection of workers and ensure that all of them receive the single
		// message we send
		var workers = new List<(SleepyWorker Worker, TaskCompletionSource<bool> Tcs)> (workerCount);
		for (var index = 0; index < workerCount; index++) {
			var tcs = new TaskCompletionSource<bool> ();
			var worker = new SleepyWorker($"worker{index + 1}", tcs);
			workers.Add ((worker, tcs));
		}

		var topic = nameof (SingleProducerSeveralSleepyConsumersPublishAsync);
		await _hub.CreateAsync (topic, _configuration, _errorWorker, workers.Select (x => x.Worker));
		// publish a single message that will be received by all workers meaning we should wait for ALL their
		// task completion sources to be done
		Assert.True(_hub.TryPublish (topic, new WorkQueuesEvent ("myID")));
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
	public async Task SingleProducerSeveralCPUConsumersPublishAsync (int workerCount)
	{
		using var cts = GetCancellationToken ();
		// create a collection of workers and ensure that all of them receive the single
		// message we send
		var workers = new List<(BackgroundThreadWorker Worker, TaskCompletionSource<bool> Tcs)> (workerCount);
		for (var index = 0; index < workerCount; index++) {
			var tcs = new TaskCompletionSource<bool> ();
			var worker = new BackgroundThreadWorker ($"worker{index + 1}", tcs);
			workers.Add ((worker, tcs));
		}

		var topic = nameof (SingleProducerSeveralSleepyConsumersPublishAsync);
		await _hub.CreateAsync (topic, _configuration, _errorWorker, workers.Select (x => x.Worker));
		// publish a single message that will be received by all workers meaning we should wait for ALL their
		// task completion sources to be done
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
	[InlineData(1, 5)]
	[InlineData(10, 5)]
	[InlineData(100, 10)]
	[InlineData(1000, 10)]
	public async Task MaxParallelWorkers (int workerCount, uint maxParallelism)
	{
		
		using var cts = GetCancellationToken ();
		// create a collection of workers and ensure that all of them receive the single
		// message we send
		var workers = new List<(BackgroundThreadWorker Worker, TaskCompletionSource<bool> Tcs)> (workerCount);
		for (var index = 0; index < workerCount; index++) {
			var tcs = new TaskCompletionSource<bool> ();
			var worker = new BackgroundThreadWorker ($"worker{index + 1}", tcs);
			workers.Add ((worker, tcs));
		}

		var topic = nameof (SingleProducerSeveralSleepyConsumersPublishAsync);
		_configuration.MaxParallelism = maxParallelism;
		await _hub.CreateAsync (topic, _configuration, _errorWorker, workers.Select (x => x.Worker));
		// publish a single message that will be received by all workers meaning we should wait for ALL their
		// task completion sources to be done
		await _hub.PublishAsync (topic, new WorkQueuesEvent ("myID"), cts.Token);
		var result = await Task.WhenAll (workers.Select (x => x.Tcs.Task));
		Assert.Equal (workerCount, result.Length);
		// assert that all did indeed return true
		foreach (bool b in result) {
			// find a nicer way to match the worker with the bool
			Assert.True (b);
		}
	}

}
