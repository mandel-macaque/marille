using Marille.Tests.Workers;

namespace Marille.Tests;

public class RegistrationTests : IDisposable {
	readonly Hub _hub;
	readonly ErrorWorker<WorkQueuesEvent> _errorWorker;
	readonly SemaphoreSlim _semaphoreSlim;

	public RegistrationTests ()
	{
		_semaphoreSlim = new(1);
		_hub = new (_semaphoreSlim);
		_errorWorker = new();
	} 
	
	public void Dispose ()
	{
		_hub.Dispose ();
		_errorWorker.Dispose ();
		_semaphoreSlim.Dispose ();
	}

	[Fact]
	public async Task SingleOneToOneCreation ()
	{
		const string topic = nameof (SingleOneToOneCreation); 
		var tcs = new TaskCompletionSource<bool> ();
		var worker = new FastWorker ("myWorkerID", tcs);
		var workers = new [] { worker };
		TopicConfiguration configuration = new() { Mode = ChannelDeliveryMode.AtMostOnceAsync };
		Assert.True (await _hub.CreateAsync (topic, configuration, _errorWorker, workers));
	}

	[Fact]
	public async Task MultipleOneToOneCreation ()
	{
		var topic = nameof (MultipleOneToOneCreation);
		var tcs = new TaskCompletionSource<bool> ();
		var worker1 = new FastWorker ("myWorkerID", tcs);
		var worker2 = new FastWorker ("myWorkerID", tcs);
		var workers = new [] { worker1, worker2 };
		TopicConfiguration configuration = new() { Mode = ChannelDeliveryMode.AtMostOnceAsync };
		Assert.False(await _hub.CreateAsync (topic, configuration, _errorWorker, workers));
	}

	[Fact]
	public async Task SingleOneToOneRegistration ()
	{
		var topic = nameof (SingleOneToOneRegistration);
		var tcs = new TaskCompletionSource<bool> ();
		var worker = new FastWorker ("myWorkerID", tcs);
		TopicConfiguration configuration = new() { Mode = ChannelDeliveryMode.AtMostOnceAsync };
		await _hub.CreateAsync (topic, configuration, _errorWorker);
		Assert.True (await _hub.RegisterAsync (topic, worker));
	}

	[Fact]
	public async Task MultipleOneToOneRegistration ()
	{
		var topic = nameof (MultipleOneToOneRegistration);
		var tcs = new TaskCompletionSource<bool> ();
		var worker1 = new FastWorker ("myWorkerID", tcs);
		var worker2 = new FastWorker ("myWorkerID", tcs);
		TopicConfiguration configuration = new() { Mode = ChannelDeliveryMode.AtMostOnceAsync };
		await _hub.CreateAsync (topic, configuration, _errorWorker);
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
		TopicConfiguration configuration = new() { Mode = ChannelDeliveryMode.AtMostOnceAsync };
		await _hub.CreateAsync (topic, configuration, _errorWorker);
		Assert.True (await _hub.RegisterAsync (topic, worker1));
		Assert.False(await _hub.RegisterAsync (topic, action));
	}

	[Fact]
	public async Task MutithreadCreate ()
	{
		var threadCount = 100;
		var results = new List<Task<bool>> (100);

		Random random = new Random ();
		var topic = nameof (MutithreadCreate );

		TopicConfiguration configuration = new() { Mode = ChannelDeliveryMode.AtMostOnceAsync };

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
				var created = await _hub.CreateAsync (topic, configuration, _errorWorker);
				tcs.TrySetResult (created);
			});
		}
		
		// release the semaphore so that we can move on
		_semaphoreSlim.Release ();
		var added = await Task.WhenAll (results);
		bool? positive = null;
		var finalResult = true;
		// ensure that we have a true and a false, that means that an && should be false
		for (var index = 0; index < threadCount; index++) {
			finalResult &= added[index];
			if (added[index] && positive is null) {
				positive = true;
				continue;
			}
			if (added[index] && positive is true) {
				Assert.Fail ("More than one addition happened.");
			}
		}
		Assert.False (finalResult);
	}

}
