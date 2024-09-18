namespace Marille; 

internal static class WorkerExtensions {

	// properties can throw (ie: NotImplementedException) so we need to catch them and use a default value. We should
	// allow to log this later.
	public static bool TryGetUseBackgroundThread<T> (this IWorker<T> worker, out bool useBackgroundThread) where T : struct
	{
		useBackgroundThread = false;
		try {
			useBackgroundThread = worker.UseBackgroundThread;
			return true;
		} catch {
			return false;
		}
	}
	
	public static bool TryGetUseBackgroundThread<T> (this IErrorWorker<T> worker, out bool useBackgroundThread) where T : struct
	{
		useBackgroundThread = false;
		try {
			useBackgroundThread = worker.UseBackgroundThread;
			return true;
		} catch {
			return false;
		}
	}

	public static async Task ConsumeThreadAsync<T> (this IWorker<T> worker, T message, 
		CancellationToken cancellationToken = default, 
		SemaphoreSlim? parallelSemaphore = null) where T : struct
	{
		_ = worker.TryGetUseBackgroundThread (out var useBackgroundThread);
		if (useBackgroundThread) {
			if (parallelSemaphore is not null)
				await parallelSemaphore.WaitAsync (cancellationToken).ConfigureAwait (false);
			// spawn a new thread to consume the message
			await Task.Run (async () => {
				try {
					await worker.ConsumeAsync (message, cancellationToken).ConfigureAwait (false);
				} finally {
					parallelSemaphore?.Release ();
				}
			}, cancellationToken);
		} else {
			await worker.ConsumeAsync (message, cancellationToken).ConfigureAwait (false);
		}
	}

	public static async Task ConsumeThreadAsync<T> (this IErrorWorker<T> worker, T message,
		Exception exception, 
		CancellationToken cancellationToken = default,
		SemaphoreSlim? parallelSemaphore = null) where T : struct
	{
		_ = worker.TryGetUseBackgroundThread (out var useBackgroundThread);
		if (useBackgroundThread) {
			if (parallelSemaphore is not null)
				await parallelSemaphore.WaitAsync (cancellationToken).ConfigureAwait (false);
			// spawn a new thread to consume the message
			await Task.Run (async () => {
				try {
					await worker.ConsumeAsync (message, exception, cancellationToken).ConfigureAwait (false);
				} finally {
					parallelSemaphore?.Release ();
				}
			}, cancellationToken);
		} else {
			await worker.ConsumeAsync (message, exception, cancellationToken).ConfigureAwait (false);
		}
	}
	
}
