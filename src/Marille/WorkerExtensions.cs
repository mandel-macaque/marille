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
}
