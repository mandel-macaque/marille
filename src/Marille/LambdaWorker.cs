namespace Marille;

internal class LambdaWorker<T> (Func<T, CancellationToken,Task> lambda, bool useBackgroundThread = false) 
	: IWorker<T> where T : struct {
	public bool UseBackgroundThread { get; } = useBackgroundThread;

	public async Task ConsumeAsync (T message, CancellationToken cancellationToken = default)
	{
		// await the lambda function so that we can wrap any exceptions in a Task
		await lambda (message, cancellationToken);
	}

	public Task OnChannelClosedAsync (string channelName, CancellationToken token = default) => Task.CompletedTask; 

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
