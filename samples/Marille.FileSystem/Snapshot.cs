using System.Diagnostics;
using System.Text;
using HeyRed.Mime;
using Serilog;

namespace Marille.FileSystem;

public class Snapshot (Guid id, string basePath) {

	Guid Id { get; } = id;
	string BasePath { get; } = basePath;
	string OriginalSourcePath => Path.Combine (BasePath, "original");
	
	public DateTimeOffset Timestamp { get; } = DateTimeOffset.Now;

	/// <summary>
	/// Returns the list of files that have modifications stored in the snapshot.
	/// </summary>
	public IEnumerable<string> Files {
		get {
			foreach (var fullPath in Directory.GetDirectories (BasePath)) {
				if (fullPath == OriginalSourcePath)
					continue;
				var directoryName = Path.GetFileName (fullPath);
				byte[] data = Convert.FromBase64String(directoryName);
				yield return Encoding.UTF8.GetString(data);
			}
		}
	}

	/// <summary>
	/// Returns the directory where the patches for a file are stored. If the directory does not exist,
	/// it will be created.
	/// </summary>
	/// <param name="path">The path whose patches we want to read/write.</param>
	/// <returns>The path to the directory that contains the patches for the file.</returns>
	public string GetDirectory (string path)
	{
		var directoryName = Convert.ToBase64String (Encoding.UTF8.GetBytes (path));
		var directory = Path.Combine (BasePath, directoryName);
		Directory.CreateDirectory (directory);
		
		Log.Debug ("Created directory {Directory} for {Path}", directory, path);
		return directory;
	}
	
	public string GetOriginalPath (string path)
	{
		var fixedPath = path.Substring (1);
		return Path.Combine (OriginalSourcePath, fixedPath);
	}
	
	public string CreateNewPatchFileName (string path)
	{
		return string.Empty;
	}

	public IEnumerable<string> GetPatches (string path)
	{
		var patchesDirectory = GetDirectory (path);
		var previousPatchFiles= Directory.GetFiles (patchesDirectory);
		Array.Sort (previousPatchFiles);
		Log.Debug ("Sorted patches for {Path} are {Patches}", path, previousPatchFiles);
		return previousPatchFiles;
	}

	static void RemoveEmptyDirs (string path)
	{
		foreach (var directory in Directory.GetDirectories(path))
		{
			RemoveEmptyDirs (directory);
			if (Directory.GetFiles (directory).Length != 0 ||
			    Directory.GetDirectories (directory).Length != 0)
				continue;
			Log.Debug ("Removing empty directory {Directory}", directory);
			Directory.Delete (directory, false);
		}
	}

	static void RemoveNotNeededFiles (string destinationPath)
	{
		foreach (var file in Directory.GetFiles(destinationPath, "*.*", SearchOption.AllDirectories)) {
			// we just want to keep the files that are of the following mime types
			try {
				var mime = MimeGuesser.GuessFileType (file);
				var remove = mime.MimeType switch {
					"application/json" => false,
					var str when str.StartsWith("text/") => false,
					_ => true,
				};
				if (!remove) 
					continue;
				try {
					Log.Debug("Removing {File} because of mime {Mime}", file, mime.MimeType);
					File.Delete(file);
				} catch (Exception exception){
					Log.Error (exception, "Error removing {File}", file);
				}
			} catch (Exception exception){
				Log.Error (exception, "Error getting mime type for {File}", file);
			}
		}
	}
	
	public void CreateOriginalSource (IEnumerable<string> paths)
	{
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
			var destinationPath = Path.Combine (OriginalSourcePath, fixedPath);
			Log.Information ("ditto {Path} {SnapshotDir}", path, destinationPath); 
			var dittoProcess = new Process {
				StartInfo = new() {
					FileName = "/usr/bin/ditto",
					Arguments = $"\"{path}\" \"{destinationPath}\"",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					CreateNoWindow = true,
				}
			};
			dittoProcess.Start ();
			Log.Information ("{Output}", dittoProcess.StandardOutput.ReadToEnd ());
			
			// This is a small performance optimization, we are going to remove all those files that we are not
			// going to be able to create a diff for. This way we use less data in the hdd. Because we want
			// to already listen to changes, we will do this in a background thread.
			_ = Task.Run (() => {
				RemoveNotNeededFiles (destinationPath);
				RemoveEmptyDirs (destinationPath);
				Log.Information ("Removed not needed files and empty directories from {Path}", destinationPath);
			});
		});
	}
}
