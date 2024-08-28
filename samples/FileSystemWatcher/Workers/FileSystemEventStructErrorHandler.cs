using Marille;
using Serilog;

namespace FileSystemWatcher.Workers;

public class FileSystemEventStructErrorHandler : IErrorWorker<FileSystemEventStruct> {

	public bool UseBackgroundThread => false;

	public Task ConsumeAsync (FileSystemEventStruct message, Exception exception, CancellationToken token = default)
	{
		// log the error and the event that caused it, there is not much we can do about it
		Log.Error (exception, "Error processing event {Event}", message);
		return Task.CompletedTask;
	}

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
