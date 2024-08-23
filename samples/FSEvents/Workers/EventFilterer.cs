using HeyRed.Mime;
using CoreServices;

namespace Marille.Workers;

public class EventFilterer(Hub hub) : IWorker<FSEvent> {
	Hub _hub = hub;
	
	public bool UseBackgroundThread => true;

	public async Task ConsumeAsync (FSEvent message, CancellationToken token = default)
	{
		if (message.Path is null)
			return;

		// ignore directories
		if (message.Flags.HasFlag (FSEventStreamEventFlags.ItemIsDir))
			return;

		// ignore those events that are not a modification or creation of a file
		if (!message.Flags.HasFlag (FSEventStreamEventFlags.ItemModified)
		    || !message.Flags.HasFlag (FSEventStreamEventFlags.ItemCreated))
			return;

		// based on the path we can do several things, first if we do know the extension of the file
		// we will use that to decide what to do. Else we will use the file command to decide what to do.
		try {
			var mime = MimeGuesser.GuessFileType (message.Path);
			if (mime.MimeType.StartsWith ("text/")) {
				await _hub.PublishAsync (nameof (FSMonitor), new TextFileChangedEvent (message));
			} else {
				Console.WriteLine ($"Unknown file type: {mime.MimeType} for path {message.Path}");
			}
		} catch (Exception ex) {
			Console.WriteLine ($"Error while guessing the mime type for {message.Path}: {ex.Message}");
		}
	}

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
