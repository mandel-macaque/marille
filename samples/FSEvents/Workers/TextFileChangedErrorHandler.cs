namespace Marille.Workers;

public class TextFileChangedErrorHandler : IErrorWorker<TextFileChangedEvent> {
	public bool UseBackgroundThread => false;
	public Task ConsumeAsync (TextFileChangedEvent message, Exception exception, CancellationToken token = default)
	{
		throw new NotImplementedException ();
	}

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
