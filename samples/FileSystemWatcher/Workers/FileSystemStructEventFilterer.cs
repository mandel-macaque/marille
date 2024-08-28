using Marille;
using Marille.FileSystem.Events;
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
		TextFileChangedEvent? textFileChangedEvent = default;

		switch (currentMessage.ChangeType) {
		case WatcherChangeTypes.Changed:
		case WatcherChangeTypes.Created: {
			if (IsValidMimeType (currentMessage.FullPath))
				textFileChangedEvent = TextFileChangedEventFactory.FromFileSystemEventStruct (currentMessage);
			break;
		}
		case WatcherChangeTypes.Deleted:
			textFileChangedEvent = TextFileChangedEventFactory.FromFileSystemEventStruct (currentMessage);
			break;
		case WatcherChangeTypes.Renamed:
			if (currentMessage.DestinationFullPath is not null && IsValidMimeType (currentMessage.DestinationFullPath))
				textFileChangedEvent = TextFileChangedEventFactory.FromFileSystemEventStruct (currentMessage);
			break;
		}

		if (textFileChangedEvent is not null)
			await _hub.PublishAsync (nameof(FileSystemWatcher), textFileChangedEvent.Value);
	}

	public override Task OnChannelClosedAsync (string channelName, CancellationToken token = default)
		=> Task.CompletedTask;
}
