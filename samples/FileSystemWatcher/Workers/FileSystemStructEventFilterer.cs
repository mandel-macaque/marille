using Marille;
using Marille.FileSystem.Workers;
using Serilog;

namespace FileSystemWatcher.Workers;

public class FileSystemStructEventFilterer(Hub hub): EventFilterer<FileSystemEventStruct> {
	readonly Hub _hub = hub;
	public override bool UseBackgroundThread => true;

	public override async Task ConsumeAsync (FileSystemEventStruct currentMessage, CancellationToken token = default)
	{
		Log.Information("Filtering event {Event}", currentMessage);

		if (currentMessage.FullPath.Contains ("/.git/")) {
			Log.Debug ("Ignoring event for .git directory {Path}", currentMessage.FullPath);
			return;
		}

		// decide what to do based on the type of change
		var textFileChangedEvent =
			TextFileChangedEventFactory.FromFileSystemEventStruct (currentMessage);

		switch (currentMessage.ChangeType) {
		case WatcherChangeTypes.Changed:
		case WatcherChangeTypes.Created: {
			// filter based of the type
			if (textFileChangedEvent is not null)
				await _hub.PublishAsync (nameof(FileSystemWatcher), textFileChangedEvent.Value);
			break;
		}
		case WatcherChangeTypes.Deleted:
			// always publish the event
		}

	}
}
