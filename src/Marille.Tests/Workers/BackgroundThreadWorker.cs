namespace Marille.Tests.Workers;

public class BackgroundThreadWorker : IWorker<WorkQueuesEvent> {

	public string Id { get; set; } = string.Empty;
	
	public TaskCompletionSource<bool> Completion { get; private set; }

	public BackgroundThreadWorker (string id, TaskCompletionSource<bool> tcs)
	{
		Id = id;
		Completion = tcs;
	}
	
	public bool UseBackgroundThread => true;
	public async Task ConsumeAsync (WorkQueuesEvent message, CancellationToken token = default)
	{
		await Task.Delay (TimeSpan.FromMilliseconds (1000));
		Completion.TrySetResult (true);
	}

	public Task OnChannelClosedAsync (string channelName, CancellationToken token = default) => Task.CompletedTask; 

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
