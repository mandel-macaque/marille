using CoreServices;

namespace Marille.Workers;

public sealed class ErrorWorker : IErrorWorker<FSEvent> {
	public bool UseBackgroundThread => false;

	public Task ConsumeAsync (FSEvent message, Exception exception, CancellationToken token = default)
	{
		throw new NotImplementedException ();
	}

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
