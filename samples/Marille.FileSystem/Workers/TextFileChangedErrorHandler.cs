using Marille.FileSystem.Events;
using Serilog;

namespace Marille.FileSystem.Workers;

public class TextFileChangedErrorHandler : IErrorWorker<TextFileChangedEvent> {
	public bool UseBackgroundThread => false;
	public Task ConsumeAsync (TextFileChangedEvent message, Exception exception, CancellationToken token = default)
	{
		Log.Error (exception, "Error processing event {Event}", message);
		return Task.CompletedTask;
	}

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
