using CoreServices;

namespace Marille.Workers;

/// <summary>
/// Worker that will write into the console the different events that are happening.
/// </summary>
public class ConsoleWriteLineWorker : IWorker<FSEvent> {
	public Task ConsumeAsync (FSEvent message, CancellationToken token = default)
	{
		Console.WriteLine($"ConsoleWriteLineWorker: Got event {message}");
		return Task.CompletedTask;
	}

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
