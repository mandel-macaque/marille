using System.Threading.Channels;

namespace Marille.Tests;

// Set of tests that focus on the pattern in which a 
// several consumers register to a queue and the compete
// to consume an event.
public class WorkQueuesTests {

	class SlowWorker : IWorker<WorkQueuesEvent> {
		public string Id { get; set; } = string.Empty;
		public Task ConsumeAsync (WorkQueuesEvent message, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException ();
		}
	}
	
	class ExceptionWorker : IWorker<WorkQueuesEvent> {
		public string Id { get; set; } = string.Empty;
		public Task ConsumeAsync (WorkQueuesEvent message, CancellationToken cancellationToken = default)
		{
			throw new NotImplementedException ();
		}
	}

	Hub _hub;
	TopicConfiguration configuration;
	readonly CancellationTokenSource cancellationTokenSource;

	public WorkQueuesTests ()
	{
		// use a simpler channel that we will use to receive the events when
		// a worker has completed its work
		_hub = new ();
		configuration = new() { Mode = ChannelDeliveryMode.AtMostOnce };
		cancellationTokenSource = new();
		cancellationTokenSource.CancelAfter (TimeSpan.FromSeconds (10));
	}

	// The simplest API usage, we register to an event for a topic with our worker
	// and the event should be consumed and added to the completed channel for use to verify
	[Fact]
	public async void SingleWorker ()
	{
		var topic = "topic";
		var tcs = new TaskCompletionSource<bool> ();
		var worker = new FastWorker ("myWorkerID", tcs);
		await _hub.CreateAsync<WorkQueuesEvent> (topic, configuration);
		await _hub.RegisterAsync (topic, worker);
		await _hub.Publish (topic, new WorkQueuesEvent ("myID"));
		Assert.True (await tcs.Task);
	}

	[Fact]
	public async void SingleAction ()
	{
		var topic = "topic";
		var tcs = new TaskCompletionSource<bool>();
		Func<WorkQueuesEvent, CancellationToken, Task> action = (_, _) =>
			Task.FromResult (tcs.TrySetResult(true));

		await _hub.CreateAsync<WorkQueuesEvent> (topic, configuration);
		Assert.True (await _hub.RegisterAsync (topic, action));
		await _hub.Publish (topic, new WorkQueuesEvent("myID"));
		Assert.True (await tcs.Task);
	}

	[Fact]
	public async void SeveralWorkers ()
	{
		string workerID = "myWorkerID";

		string topic1 = "topic1";
		var tcsWorker1 = new TaskCompletionSource<bool> ();
		await _hub.CreateAsync<WorkQueuesEvent> (topic1, configuration);

		string topic2 = "topic2";
		var tcsWorker2 = new TaskCompletionSource<bool> ();
		await _hub.CreateAsync<WorkQueuesEvent> (topic2, configuration);
		
		// ensure that each of the workers will receive data for the topic it is interested
		var worker1 = new FastWorker (workerID, tcsWorker1);
		Assert.True (await _hub.RegisterAsync (topic1, worker1));

		Func<WorkQueuesEvent, CancellationToken, Task> worker2 = (_, _) 
			=> Task.FromResult (tcsWorker2.TrySetResult(true));
		
		Assert.True (await _hub.RegisterAsync (topic2, worker2));

		// publish two both topics and ensure that each of the workers gets teh right data
		await _hub.Publish (topic1, new WorkQueuesEvent ("1"));

		// we should only get the event in topic one, the second topic should be ignored
		Assert.True (await tcsWorker1.Task);
		Assert.False (tcsWorker2.Task.IsCompleted);
	}

	[Fact]
	public void SlowFastWorker () { }
}
