namespace Marille.Tests;

public class CancellationTests {

	Hub _hub;
	TopicConfiguration configuration;

	public CancellationTests ()
	{
		_hub = new ();
		configuration = new();
	}
	[Fact]
	public async Task CloseSingleWorkerNoEvents ()
	{
		configuration.Mode = ChannelDeliveryMode.AtLeastOnce;
		var topic = "topic";
		var tcs = new TaskCompletionSource<bool> ();
		var worker = new BlockingWorker(tcs);
		await _hub.CreateAsync<WorkQueuesEvent> (topic, configuration);
		await _hub.RegisterAsync (topic, worker);
		tcs.SetResult (true);
		// publish no messages, just close the worker
		await _hub.CloseAsync<WorkQueuesEvent> (topic);
		Assert.Equal (0, worker.ConsumedCount);
	}
	
	[Fact]
	public async Task CloseSingleWorkerFlushedEvents ()
	{
		var eventCount = 100;
		configuration.Mode = ChannelDeliveryMode.AtLeastOnce;
		var topic = "topic";
		var tcs = new TaskCompletionSource<bool> ();
		var worker = new BlockingWorker(tcs);
		await _hub.CreateAsync<WorkQueuesEvent> (topic, configuration);
		await _hub.RegisterAsync (topic, worker);
		// send several events, they will be blocked until the worker is ready to consume them
		for (var i = 0; i < eventCount; i++) {
			await _hub.Publish (topic, new WorkQueuesEvent($"myID{i}"));
		}
		tcs.SetResult (true);
		// publish no messages, just close the worker
		await _hub.CloseAsync<WorkQueuesEvent> (topic);
		Assert.Equal (eventCount, worker.ConsumedCount);
	}

	[Fact]
	public async Task CloseAllWorkersNoEvents ()
	{
		var eventCount = 100;
		configuration.Mode = ChannelDeliveryMode.AtLeastOnce;
		var topic1 = "topic1";
		var tcs1 = new TaskCompletionSource<bool> ();
		var worker1 = new BlockingWorker(tcs1);
		await _hub.CreateAsync<WorkQueuesEvent> (topic1, configuration);
		await _hub.RegisterAsync (topic1, worker1);
		
		var topic2 = "topic2";
		var tcs2 = new TaskCompletionSource<bool> ();
		var worker2 = new BlockingWorker(tcs1);
		await _hub.CreateAsync<WorkQueuesEvent> (topic2, configuration);
		await _hub.RegisterAsync (topic2, worker2);
		
		for (var i = 0; i < eventCount; i++) {
			await _hub.Publish (topic1, new WorkQueuesEvent($"myID{i}"));
			await _hub.Publish (topic2, new WorkQueuesEvent($"myID{i}"));
		}
		
		tcs1.SetResult (true);
		tcs2.SetResult (true);

		// publish no messages, just close the worker
		await _hub.CloseAsync<WorkQueuesEvent> (topic1);
		Assert.Equal (eventCount, worker1.ConsumedCount);
		Assert.Equal (eventCount, worker2.ConsumedCount);
	}
	
	[Fact]
	public async Task CloseAllWorkersFlushedEvents ()
	{
		configuration.Mode = ChannelDeliveryMode.AtLeastOnce;
		var topic1 = "topic1";
		var tcs1 = new TaskCompletionSource<bool> ();
		var worker1 = new BlockingWorker(tcs1);
		await _hub.CreateAsync<WorkQueuesEvent> (topic1, configuration);
		await _hub.RegisterAsync (topic1, worker1);
		
		var topic2 = "topic2";
		var tcs2 = new TaskCompletionSource<bool> ();
		var worker2 = new BlockingWorker(tcs1);
		await _hub.CreateAsync<WorkQueuesEvent> (topic2, configuration);
		await _hub.RegisterAsync (topic2, worker2);
		
		tcs1.SetResult (true);
		tcs2.SetResult (true);

		// publish no messages, just close the worker
		await _hub.CloseAsync<WorkQueuesEvent> (topic1);
		Assert.Equal (0, worker1.ConsumedCount);
		Assert.Equal (0, worker2.ConsumedCount);
	}
}
