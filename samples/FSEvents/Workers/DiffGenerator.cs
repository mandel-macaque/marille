using Serilog;

namespace Marille.Workers;

public class DiffGenerator : IWorker<TextFileChangedEvent> {
	public bool UseBackgroundThread => true;
	public Task ConsumeAsync (TextFileChangedEvent message, CancellationToken token = default)
	{
		Log.Information ("Generating diff for {Event}", message);
		return Task.CompletedTask;
	}

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
