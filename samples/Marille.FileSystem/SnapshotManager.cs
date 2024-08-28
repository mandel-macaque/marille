using Serilog;
using UuidExtensions;

namespace Marille.FileSystem;

public class SnapshotManager {

	public static Snapshot Create (string basePath, List<string> paths)
	{
		// create the needed dirs and return a new snapshot
		var snapshotId = Uuid7.Guid();
		var snapshotDir = Path.Combine (basePath, snapshotId.ToString()!);

		Directory.CreateDirectory (snapshotDir);
		Log.Information ("Created snapshot directory {BaseDir}", snapshotDir);
		
		var snapshot = new Snapshot (snapshotId, snapshotDir);
		snapshot.CreateOriginalSource (paths);
		
		return snapshot;

	}
}
