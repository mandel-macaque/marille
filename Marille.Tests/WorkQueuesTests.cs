using System.Threading.Channels;

namespace Marille.Tests;

// Set of tests that focus on the pattern in which a 
// several consumers register to a queue and the compete
// to consume an event.
public class WorkQueuesTests {
	record WorkQueuesEvent (string Id);

	record WorkDoneEvent (string WorkerId);

	class FastWorker : IWorker<WorkQueuesEvent> {
		public string Id { get; set; } = string.Empty;
		public AutoResetEvent Event { get; private set; }

		public FastWorker (string id, AutoResetEvent @event)
		{
			Id = id;
			Event = @event;
		}

		public Task ConsumeAsync (WorkQueuesEvent message, CancellationToken cancellationToken = default)
			=> Task.FromResult (Event.Set ());
	}

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

	class WorkQueuesTestHub : Hub { }

	Hub _hub;
	TopicConfiguration configuration;
	readonly CancellationTokenSource cancellationTokenSource;

	public WorkQueuesTests ()
	{
		// use a simpler channel that we will use to receive the events when
		// a worker has completed its work
		_hub = new WorkQueuesTestHub ();
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
		var reset = new AutoResetEvent (false);
		var worker = new FastWorker ("myWorkerID", reset);
		_hub.TryCreate<WorkQueuesEvent> (topic, configuration);
		_hub.TryRegister (topic, worker);
		await _hub.Publish (topic, new WorkQueuesEvent ("myID"));
		Assert.True (reset.WaitOne(10));
	}

	[Fact]
	public async void SingleAction ()
	{
		var topic = "topic";
		var reset = new AutoResetEvent (false);
		Func<WorkQueuesEvent, CancellationToken, Task> action = (_, _) =>
			Task.FromResult (reset.Set ());

		_hub.TryCreate<WorkQueuesEvent> (topic, configuration);
		Assert.True (_hub.TryRegister (topic, action));
		await _hub.Publish (topic, new WorkQueuesEvent("myID"));
		Assert.True (reset.WaitOne(10));
	}

	[Fact]
	public async void SeveralWorkers ()
	{
		string workerID = "myWorkerID";

		string topic1 = "topic1";
		var resetWorker1 = new AutoResetEvent (false);
		_hub.TryCreate<WorkQueuesEvent> (topic1, configuration);

		string topic2 = "topic2";
		var resetWorker2 = new AutoResetEvent (false);
		_hub.TryCreate<WorkQueuesEvent> (topic2, configuration);
		
		// ensure that each of the workers will receive data for the topic it is interested
		var worker1 = new FastWorker (workerID, resetWorker1);
		Assert.True (_hub.TryRegister (topic1, worker1));

		Func<WorkQueuesEvent, CancellationToken, Task> worker2 = (_, _) => {
			return Task.FromResult (resetWorker2.Set ());
		};
		Assert.True (_hub.TryRegister (topic2, worker2));

		// publish two both topics and ensure that each of the workers gets teh right data
		await _hub.Publish (topic1, new WorkQueuesEvent ("1"));

		// we should only get the event in topic one, the second topic should be ignored
		Assert.True (resetWorker1.WaitOne(10));
		Assert.False (resetWorker2.WaitOne(10));
	}

	[Fact]
	public void SlowFastWorker () { }
}
