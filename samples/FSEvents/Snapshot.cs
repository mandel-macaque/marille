namespace Marille;

public class Snapshot (Guid id, string basePath) {

	public Guid Id { get; private set; } = id;
	public string BasePath { get; private set; } = basePath;
	public string OriginalSourcePath => Path.Combine (basePath, "original");
}
