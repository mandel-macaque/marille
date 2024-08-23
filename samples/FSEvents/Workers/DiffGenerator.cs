namespace Marille.Workers;

public class DiffGenerator : IWorker<TextFileChangedEvent> {
	public bool UseBackgroundThread => true;
	public Task ConsumeAsync (TextFileChangedEvent message, CancellationToken token = default)
	{
		Console.WriteLine($"##### Text File: {message.RawEvent.Path}");
		return Task.CompletedTask;
	}

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
