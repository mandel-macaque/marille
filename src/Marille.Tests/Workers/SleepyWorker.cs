namespace Marille.Tests.Workers;

public class SleepyWorker : IWorker<WorkQueuesEvent> {
	Random random = new Random ();
	public string Id { get; set; } = string.Empty;
	public TaskCompletionSource<bool> Completion { get; private set; }

	public SleepyWorker (string id, TaskCompletionSource<bool> tcs)
	{
		Id = id;
		Completion = tcs;
	}

	public bool UseBackgroundThread => false;

	public async Task ConsumeAsync (WorkQueuesEvent message, CancellationToken cancellationToken = default)
	{
		var sleep = random.Next (1000);
		await Task.Delay (TimeSpan.FromMilliseconds (sleep));
		Completion.TrySetResult (true);
	}

	public Task OnChannelClosedAsync (string channelName, CancellationToken token = default) => Task.CompletedTask; 

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
