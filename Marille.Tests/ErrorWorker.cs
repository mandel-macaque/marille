namespace Marille.Tests;

public class ErrorWorker<T> : IErrorWorker<T> where T :  struct {

	SemaphoreSlim _semaphoreSlim = new (1);
	int _consumedCount;
	public int ConsumedCount => _consumedCount;
	public List<(T Message, Exception Exception)> ConsumedMessages { get; private set; } = new();

	public TaskCompletionSource<bool> TaskCompletionSource { get; private set; } = new();
	
	public ErrorWorker () {}
	
	public ErrorWorker (TaskCompletionSource<bool> tcs)
	{
		TaskCompletionSource = tcs;
	}
	
	public async Task ConsumeAsync (T message, Exception exception, CancellationToken token = default)
	{
		await _semaphoreSlim.WaitAsync ();
		try {
			ConsumedMessages.Add ((message, exception));
			Interlocked.Increment (ref _consumedCount);
			TaskCompletionSource.TrySetResult (true);
		} finally {
			_semaphoreSlim.Release ();
		}
	}
}