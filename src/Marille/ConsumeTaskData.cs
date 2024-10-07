namespace Marille;

internal readonly struct ConsumeTaskData : IDisposable, IAsyncDisposable {
	
	public Task? Task { get; init; } = null;
	public SemaphoreSlim? Semaphore { get; init; } = null;
	
	public ConsumeTaskData () { }

	public void Dispose()
	{
		Task?.Dispose ();
		Semaphore?.Dispose ();
	}

	public async ValueTask DisposeAsync()
	{
		if (Task is not null) await CastAndDispose (Task);
		if (Semaphore is not null) await CastAndDispose (Semaphore);

		return;

		static async ValueTask CastAndDispose (IDisposable resource)
		{
			if (resource is IAsyncDisposable resourceAsyncDisposable)
				await resourceAsyncDisposable.DisposeAsync ();
			else
				resource.Dispose ();
		}
	}

}
