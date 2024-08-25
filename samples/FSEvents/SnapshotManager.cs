using System.Diagnostics;
using Serilog;
using UuidExtensions;

namespace Marille;

public class SnapshotManager {

	public static Snapshot Create (string basePath, List<string> paths)
	{
		// create the needed dirs and return a new snapshot
		var snapshotId = Uuid7.Guid();
		var snapshotDir = Path.Combine (basePath, snapshotId.ToString()!);

		Directory.CreateDirectory (snapshotDir);
		Log.Information ("Created snapshot directory {BaseDir}", snapshotDir);
		
		var snapshot = new Snapshot (snapshotId, snapshotDir);

		// copy all the directories. If you google around you are going to find a collection os weird ways to do this
		// by iterating over the files and directories, yikes. We know we are on macOS so we can use the ditto command
		// which will keep the metadata of the files and directories and should be more efficient than a c# implementation.
		Parallel.ForEach (paths, path => {
			// little trick here, we need to remove the first / from the path so that path.combine works, else it will
			// return the second path.
			var fixedPath = path.Substring (1);
			if (string.IsNullOrEmpty (fixedPath)) {
				Log.Information ("Ignoring empty path");
				return;
			}
			var destinationPath = Path.Combine (snapshot.OriginalSourcePath, fixedPath);
			Log.Information ("ditto {Path} {SnapshotDir}", path, destinationPath); 
			var dittoProcess = new Process {
				StartInfo = new ProcessStartInfo {
					FileName = "/usr/bin/ditto",
					Arguments = $"\"{path}\" \"{destinationPath}\"",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					CreateNoWindow = true,
				}
			};
			dittoProcess.Start ();
			Log.Information ("{Output}", dittoProcess.StandardOutput.ReadToEnd ());
		});

		return snapshot;

	}
}
