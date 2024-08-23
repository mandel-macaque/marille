using Marille.Tests.Workers;

namespace Marille.Tests;

public class CancellationTests : IDisposable {
	readonly ErrorWorker<WorkQueuesEvent> _errorWorker;
	readonly Hub _hub;
	readonly SemaphoreSlim _semaphoreSlim;
	TopicConfiguration _configuration;

	public CancellationTests ()
	{
		_semaphoreSlim = new (1);
		_errorWorker = new();
		_hub = new (_semaphoreSlim);
		_configuration = new();
	}
	
	public void Dispose ()
	{
		_errorWorker.Dispose ();
		_hub.Dispose ();
		_semaphoreSlim.Dispose ();
	}

	[Fact]
	public async Task CloseSingleWorkerNoEvents ()
	{
		_configuration.Mode = ChannelDeliveryMode.AtLeastOnceAsync;
		var topic = nameof (CloseSingleWorkerNoEvents);
		var tcs = new TaskCompletionSource<bool> ();
		var worker = new BlockingWorker(tcs);
		await _hub.CreateAsync (topic, _configuration, _errorWorker);
		await _hub.RegisterAsync (topic, worker);
		tcs.SetResult (true);
		// publish no messages, just close the worker
		await _hub.CloseAsync<WorkQueuesEvent> (topic);
		Assert.Equal (0, worker.ConsumedCount);
		Assert.Equal (0, _errorWorker.ConsumedCount);
	}
	
	[Fact]
	public async Task CloseSingleWorkerFlushedEvents ()
	{
		var eventCount = 100;
		_configuration.Mode = ChannelDeliveryMode.AtLeastOnceAsync;
		var topic = nameof (CloseSingleWorkerFlushedEvents);
		var tcs = new TaskCompletionSource<bool> ();
		var worker = new BlockingWorker(tcs);
		await _hub.CreateAsync (topic, _configuration, _errorWorker);
		await _hub.RegisterAsync (topic, worker);
		// send several events, they will be blocked until the worker is ready to consume them
		for (var i = 0; i < eventCount; i++) {
			await _hub.PublishAsync (topic, new WorkQueuesEvent($"myID{i}"));
		}
		tcs.SetResult (true);
		// publish no messages, just close the worker
		await _hub.CloseAsync<WorkQueuesEvent> (topic);
		Assert.NotEqual (0, worker.ConsumedCount);
		Assert.Equal (0, _errorWorker.ConsumedCount);
	}

	[Fact]
	public async Task CloseAllWorkersFlushedEvents ()
	{
		var eventCount = 100;
		_configuration.Mode = ChannelDeliveryMode.AtLeastOnceAsync;
		var topic1 = nameof (CloseAllWorkersFlushedEvents);
		var tcs1 = new TaskCompletionSource<bool> ();
		var worker1 = new BlockingWorker(tcs1);
		await _hub.CreateAsync (topic1, _configuration, _errorWorker);
		await _hub.RegisterAsync (topic1, worker1);

		var topic2 = $"{topic1}2";
		var tcs2 = new TaskCompletionSource<bool> ();
		var worker2 = new BlockingWorker(tcs1);
		await _hub.CreateAsync (topic2, _configuration, _errorWorker);
		await _hub.RegisterAsync (topic2, worker2);
		
		for (var i = 0; i < eventCount; i++) {
			await _hub.PublishAsync (topic1, new WorkQueuesEvent($"myID{i}"));
			await _hub.PublishAsync (topic2, new WorkQueuesEvent($"myID{i}"));
		}
		
		tcs1.SetResult (true);
		tcs2.SetResult (true);

		// publish no messages, just close the worker
		await _hub.CloseAsync<WorkQueuesEvent> (topic1);
		Assert.NotEqual (0, worker1.ConsumedCount);
		Assert.NotEqual (0, worker2.ConsumedCount);
		Assert.Equal (0, _errorWorker.ConsumedCount);
	}
	
	[Fact]
	public async Task CloseAllWorkersNoEvents ()
	{
		_configuration.Mode = ChannelDeliveryMode.AtLeastOnceAsync;
		var topic1 = nameof (CloseAllWorkersNoEvents);
		var tcs1 = new TaskCompletionSource<bool> ();
		var worker1 = new BlockingWorker(tcs1);
		await _hub.CreateAsync (topic1, _configuration, _errorWorker);
		await _hub.RegisterAsync (topic1, worker1);
		
		var topic2 = $"{topic1}2";
		var tcs2 = new TaskCompletionSource<bool> ();
		var worker2 = new BlockingWorker(tcs2);
		await _hub.CreateAsync<WorkQueuesEvent> (topic2, _configuration, _errorWorker);
		await _hub.RegisterAsync (topic2, worker2);
		
		tcs1.SetResult (true);
		tcs2.SetResult (true);

		// publish no messages, just close the worker
		await _hub.CloseAsync<WorkQueuesEvent> (topic1);
		Assert.Equal (0, worker1.ConsumedCount);
		Assert.Equal (0, worker2.ConsumedCount);
		Assert.Equal (0, _errorWorker.ConsumedCount);
	}

	[Fact]
	public async Task MultithreadedClose ()
	{
		var threadCount = 100;
		var results = new List<Task<bool>> (100);

		Random random = new Random ();
		
		// create the topic and then try to close if from several threads ensuring that only one of them
		// closes the channel.
		_configuration.Mode = ChannelDeliveryMode.AtLeastOnceAsync;
		var topic = nameof (MultithreadedClose);
		await _hub.CreateAsync (topic, _configuration, _errorWorker);
		
		// block the closing until we have created all the needed threads
		await _semaphoreSlim.WaitAsync ();
		
		for (var index = 0; index < threadCount; index++) {
			var tcs = new TaskCompletionSource<bool> ();
			results.Add (tcs.Task);
			// try to register from diff threads and ensure there are no unexpected issues
			// this means that we DO NOT have two true values
			// DO NOT AWAIT THE TASKS OR ELSE YOU WILL DEADLOCK
#pragma warning disable CS4014 
			Task.Run (async () => {
#pragma warning restore CS4014
				// random sleep to ensure that the other thread is also trying to create
				var sleep = random.Next (1000);
				await Task.Delay (TimeSpan.FromMilliseconds (sleep));
				var closed = await _hub.CloseAsync <WorkQueuesEvent> (topic);
				tcs.TrySetResult (closed);
			});
		}
		
		_semaphoreSlim.Release ();
		var closed = await Task.WhenAll (results);
		bool? positive = null;
		var finalResult = true;
		// ensure that we have a true and a false, that means that an && should be false
		for (var index = 0; index < threadCount; index++) {
			finalResult &= closed[index];
			if (closed[index] && positive is null) {
				positive = true;
				continue;
			}
			if (closed[index] && positive is true) {
				Assert.Fail ("More than one close happened.");
			}
		}
		Assert.False (finalResult);
		Assert.Equal (0, _errorWorker.ConsumedCount);
	}

	[Fact]
	public async Task CloseAllChannelsAsync ()
	{
		// build several channels and then close them all, this should ensure that all the workers
		// have consume all the messages
		var eventCount = 100;
		var list = new List<Task> (200);

		_configuration.Mode = ChannelDeliveryMode.AtLeastOnceAsync;
		var topic1 = nameof (CloseAllChannelsAsync);
		var tcs1 = new TaskCompletionSource<bool> ();
		var worker1 = new BlockingWorker(tcs1);
		await _hub.CreateAsync<WorkQueuesEvent> (topic1, _configuration, _errorWorker);
		await _hub.RegisterAsync (topic1, worker1);
		
		var topic2 = $"{topic1}2";
		var tcs2 = new TaskCompletionSource<bool> ();
		var worker2 = new BlockingWorker(tcs2);
		await _hub.CreateAsync<WorkQueuesEvent> (topic2, _configuration, _errorWorker);
		await _hub.RegisterAsync (topic2, worker2);
		
		for (var index = 0; index < eventCount; index++) {
			await _hub.PublishAsync (topic1, new WorkQueuesEvent($"myID{index}"));
			await _hub.PublishAsync (topic2, new WorkQueuesEvent($"myID{index}"));
		}
		
		// we are blocking the consume of the channels
		Assert.True(tcs1.TrySetResult(true));
		Assert.True(tcs2.TrySetResult(true));

		// close the hub, should throw no cancellation token exceptions and events should have been processed
		await _hub.CloseAllAsync ();
		Assert.Equal (100, worker1.ConsumedCount);
		Assert.Equal (100, worker2.ConsumedCount);
	}
}
