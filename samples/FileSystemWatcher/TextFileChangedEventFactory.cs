using Marille.FileSystem.Events;

namespace FileSystemWatcher;

public static class TextFileChangedEventFactory {

	public static TextFileChangedEvent? FromFileSystemEventStruct (FileSystemEventStruct rawEvent)
	{
		var flag = rawEvent.ChangeType switch {
			WatcherChangeTypes.Changed => TextFileChangedType.ItemModified,
			WatcherChangeTypes.Created => TextFileChangedType.ItemCreated,
			WatcherChangeTypes.Deleted => TextFileChangedType.ItemRemoved,
			WatcherChangeTypes.Renamed => TextFileChangedType.ItemRenamed,
			_ => TextFileChangedType.Unknown
		};

		return flag == TextFileChangedType.ItemRenamed
			? new TextFileChangedEvent (rawEvent.FullPath, rawEvent.DestinationFullPath!)
			: new TextFileChangedEvent (rawEvent.FullPath, flag);
	}
}
