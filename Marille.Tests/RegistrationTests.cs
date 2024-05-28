namespace Marille.Tests;

public class RegistrationTests {

	Hub _hub;

	public RegistrationTests ()
	{
		_hub = new ();
	} 

	[Fact]
	public async Task SingleOneToOneCreation ()
	{
		var topic = nameof (SingleOneToOneCreation); 
		var tcs = new TaskCompletionSource<bool> ();
		var worker = new FastWorker ("myWorkerID", tcs);
		var workers = new [] { worker };
		TopicConfiguration configuration = new() { Mode = ChannelDeliveryMode.AtMostOnce };
		Assert.True (await _hub.CreateAsync (topic, configuration, workers));
	}

	[Fact]
	public async Task MultipleOneToOneCreation ()
	{
		var topic = nameof (MultipleOneToOneCreation);
		var tcs = new TaskCompletionSource<bool> ();
		var worker1 = new FastWorker ("myWorkerID", tcs);
		var worker2 = new FastWorker ("myWorkerID", tcs);
		var workers = new [] { worker1, worker2 };
		TopicConfiguration configuration = new() { Mode = ChannelDeliveryMode.AtMostOnce };
		Assert.False(await _hub.CreateAsync (topic, configuration, workers));
	}

	[Fact]
	public async Task SingleOneToOneRegistration ()
	{
		var topic = nameof (SingleOneToOneRegistration);
		var tcs = new TaskCompletionSource<bool> ();
		var worker = new FastWorker ("myWorkerID", tcs);
		var workers = new [] { worker };
		TopicConfiguration configuration = new() { Mode = ChannelDeliveryMode.AtMostOnce };
		await _hub.CreateAsync<WorkQueuesEvent> (topic, configuration);
		Assert.True (await _hub.RegisterAsync (topic, worker));
	}

	[Fact]
	public async Task MultipleOneToOneRegistration ()
	{
		var topic = nameof (MultipleOneToOneRegistration);
		var tcs = new TaskCompletionSource<bool> ();
		var worker1 = new FastWorker ("myWorkerID", tcs);
		var worker2 = new FastWorker ("myWorkerID", tcs);
		TopicConfiguration configuration = new() { Mode = ChannelDeliveryMode.AtMostOnce };
		await _hub.CreateAsync<WorkQueuesEvent> (topic, configuration);
		Assert.True (await _hub.RegisterAsync (topic, worker1));
		Assert.False(await _hub.RegisterAsync (topic, worker2));
	}

	[Fact]
	public async Task MultipleOneToOneRegistrationWithLambda ()
	{
		var topic = nameof (MultipleOneToOneRegistrationWithLambda);
		var tcs = new TaskCompletionSource<bool> ();
		var worker1 = new FastWorker ("myWorkerID", tcs);
		Func<WorkQueuesEvent, CancellationToken, Task> action = (_, _) =>
			Task.FromResult (tcs.TrySetResult(true));
		TopicConfiguration configuration = new() { Mode = ChannelDeliveryMode.AtMostOnce };
		await _hub.CreateAsync<WorkQueuesEvent> (topic, configuration);
		Assert.True (await _hub.RegisterAsync (topic, worker1));
		Assert.False(await _hub.RegisterAsync (topic, action));
	}
}
