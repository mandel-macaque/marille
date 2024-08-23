using HeyRed.Mime;
using CoreServices;
using Serilog;

namespace Marille.Workers;

public class EventFilterer(Hub hub) : IWorker<FSEvent> {
	Hub _hub = hub;
	
	public bool UseBackgroundThread => true;

	public async Task ConsumeAsync (FSEvent message, CancellationToken token = default)
	{
		Log.Information("Filtering event {Event}", message);

		if (message.Path is null) {
			Log.Debug ("Ignoring event with null path");
			return;
		}

		// ignore directories
		if (message.Flags.HasFlag (FSEventStreamEventFlags.ItemIsDir)) {
			Log.Debug ("Ignoring event for directory {Path}", message.Path);
			return;
		}

		// ignore those events that are not a modification or creation of a file
		if (!message.Flags.HasFlag (FSEventStreamEventFlags.ItemModified)
		    || !message.Flags.HasFlag (FSEventStreamEventFlags.ItemCreated)) {
			Log.Debug ("Ignoring event for file {Path} with flags {Flags}", message.Path, message.Flags);
			return;
		}
		// based on the path we can do several things, first if we do know the extension of the file
		// we will use that to decide what to do. Else we will use the file command to decide what to do.
		var mime = MimeGuesser.GuessFileType (message.Path);
		Log.Information ("Mime type for {Path} is {Mime}", message.Path, mime.MimeType);
		await (mime.MimeType switch {
			"application/json" => _hub.PublishAsync (nameof (FSMonitor), new TextFileChangedEvent (message)),
			var str when str.StartsWith("text/") => _hub.PublishAsync (nameof (FSMonitor), new TextFileChangedEvent (message)),
			_ => ValueTask.CompletedTask,
		});
	}

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
