namespace FileSystemWatcher;

public struct FileSystemEventStruct {

	public WatcherChangeTypes ChangeType { get; }
	public string FullPath { get; } = string.Empty;
	public string? DestinationFullPath { get; private set;  } = null;

	public FileSystemEventStruct (WatcherChangeTypes changeType, string fullPath)
	{
		ChangeType = changeType;
		FullPath = fullPath;
		DestinationFullPath = null;
	}

	public FileSystemEventStruct (string fullPath, string destinationFullPath)
	{
		ChangeType = WatcherChangeTypes.Renamed;
		FullPath = fullPath;
		DestinationFullPath = destinationFullPath;
	}

	public static FileSystemEventStruct FromEventArgs (FileSystemEventArgs args)
	{
		return new(args.ChangeType, args.FullPath);
	}

	public static FileSystemEventStruct FromRenamedEventArgs (RenamedEventArgs args)
	{
		return new FileSystemEventStruct (args.OldFullPath, args.FullPath);
	}

	public override string ToString()
	{
		return $"[FileSystemEventStruct {nameof(ChangeType)}={ChangeType}, {nameof(FullPath)}={FullPath}, {nameof(DestinationFullPath)}={DestinationFullPath}";
	}
}
