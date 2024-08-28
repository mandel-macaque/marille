namespace Marille.FileSystem.Events;

public struct TextFileChangedEvent {
	public TextFileChangedType Flags { get; private set; }
	public string? Path { get; private set; }
	public string? DestinationPath { get; private set;  } = null;

	public TextFileChangedEvent (string path, TextFileChangedType flags)
	{
		Path = path;
		Flags = flags;
	}

	public TextFileChangedEvent (string fromPath, string toPath)
	{
		Path = fromPath;
		DestinationPath = toPath;
		Flags = TextFileChangedType.ItemRenamed;
	}

	public override string ToString ()
	{
		return $"[TextFileChangedEvent {nameof (Flags)}={Flags}, {nameof (Path)}={Path}, {nameof (DestinationPath)}={DestinationPath}]";
	}
}
