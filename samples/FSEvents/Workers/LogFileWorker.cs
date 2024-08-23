using CoreServices;

namespace Marille.Workers;

/// <summary>
/// This is a worker that will write all the changes into a log file. This worker is taking advantage of the fact
/// that the channel that is created in this application is using the AtLeastOnceSync delivery mode.
///
///
/// When using the AtLeastOnceSync events are processed IN ORDER and the next event will not be processed until
/// the current one is completed, that means that we can safely write the events into a log file without
/// having to worry about the threading issues that might happen. This implementation without the AtLeastOnceSync
/// semantic is a TERRIBLE idea.
/// 
/// </summary>
public sealed class LogFileWorker (string filePath) : IWorker<FSEvent> {

	readonly StreamWriter _fileWriter = new (filePath);

	public bool UseBackgroundThread => false;

	public async Task ConsumeAsync (FSEvent message, CancellationToken token = default)
	{
		await _fileWriter.WriteLineAsync  ($"LogFileWorker: {message}");
		await _fileWriter.FlushAsync (token);
	}

	public void Dispose ()
	{
		_fileWriter.Dispose ();
	}

	public async ValueTask DisposeAsync ()
	{
		await _fileWriter.DisposeAsync ();
	}
}
