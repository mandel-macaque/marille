using CoreServices;

namespace Marille.Workers;

/// <summary>
/// Worker that will write into the console the different events that are happening.
/// </summary>
public class LogEventToConsole : IWorker<FSEvent> {

	public bool UseBackgroundThread => false;

	public Task ConsumeAsync (FSEvent message, CancellationToken token = default)
	{
		Console.WriteLine($"LogEventToConsole: Got event {message}");
		return Task.CompletedTask;
	}

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
