using System.Text;

using DiffMatchPatch;
using Serilog;
using UuidExtensions;

namespace Marille.Workers;

public class DiffGenerator (Snapshot snapshot) : IWorker<TextFileChangedEvent> {
	public bool UseBackgroundThread => true;

	async Task<string> CalculateDiff (string patchesDirectory, string originalPath, string currentPath, CancellationToken token = default)
	{
		// get the previous content or an empty string if the fle did not exist
		var originalContent = string.Empty;
		if (File.Exists (originalPath))
			originalContent = await File.ReadAllTextAsync (originalPath, token);

		var currentContent = await File.ReadAllTextAsync (currentPath, token);
		
		// there is a reason why we are using uuid7 and it is because we can lexicographically sort the patches
		// With that information, we are going to do the following:
		// 1. Get all the patches that have been created for a file and sort them by the patch id
		// 2. Load the original content of the file
		// 3. Apply all the patches to the original content
		// 4. Generate the diff with the patch of the original content and the current content
		// 5. Return the new patch to be stored in the directory
		var previousPatchFiles= Directory.GetFiles (patchesDirectory);
		Array.Sort (previousPatchFiles);
		Log.Debug ("Sorted patches for {Path} are {Patches}", currentPath, previousPatchFiles);

		var dmp = new diff_match_patch();
		foreach (string patchFile in previousPatchFiles) {
			Log.Information ("Applying patch {Patch}", patchFile);
			var patchContent = await File.ReadAllTextAsync (patchFile, token);
			var oldPatches = dmp.patch_fromText (patchContent);
			// from the docs: Applies a list of patches to text1. The first element of the return value is the newly
			// patched text. The second element is an array of true/false values indicating which of the patches were
			// successfully applied
			originalContent = dmp.patch_apply (oldPatches, originalContent)[0] as string;
		}

		// create the diff vs the patched content, that way we have a diff with the very last diff
		var diffs = dmp.diff_main (originalContent, currentContent);
		var patches = dmp.patch_make (originalContent, diffs);
		var patchText = dmp.patch_toText (patches);
		return patchText;
	}
	
	public async Task ConsumeAsync (TextFileChangedEvent message, CancellationToken token = default)
	{
		// ignore events without a path
		if (message.RawEvent.Path is null)
			return;
		Log.Information ("Generating diff for {Event}", message);

		// convert the path to a base64 string which we can use to store the diff in a directory
		var directoryName = Convert.ToBase64String (Encoding.UTF8.GetBytes (message.RawEvent.Path));
		var directory = Path.Combine (snapshot.BasePath, directoryName);
		Directory.CreateDirectory (directory);
		
		Log.Information ("Created directory for {Path} to be {Directory}", 
			message.RawEvent.Path, directory);
		
		var fixedPath = message.RawEvent.Path.Substring (1);
		var originalPath = Path.Combine (snapshot.OriginalSourcePath, fixedPath);

		var patchText = await CalculateDiff (directory, originalPath, message.RawEvent.Path, token);
		Log.Debug ("Diff for {Path} is {Diff}", message.RawEvent.Path, patchText);
		
		var patchId = Uuid7.Guid();
		var patchPath = Path.Combine (directory, patchId.ToString());
		
		Log.Information ("Writing diff to {Path}", patchPath);
		await File.WriteAllTextAsync (patchPath, patchText, token);
	}

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
