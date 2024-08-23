namespace Marille;

internal class LambdaErrorWorker<T> (Func<T, Exception, CancellationToken, Task> lambda, 
	bool useBackgroundThread = false) : IErrorWorker<T> where T : struct {
	
	public bool UseBackgroundThread { get; } = useBackgroundThread;
	public Task ConsumeAsync (T message, Exception exception, CancellationToken cancellationToken = default)
		=> lambda (message, exception, cancellationToken);

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
