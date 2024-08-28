using DiffMatchPatch;
using Marille.FileSystem.Events;
using Serilog;
using UuidExtensions;

namespace Marille.FileSystem.Workers;

public class DiffGenerator (Snapshot snapshot) : IWorker<TextFileChangedEvent> {
	public bool UseBackgroundThread => true;
	diff_match_patch _dmp = new();

	async Task<string> ApplyPatches (string originalPath, string currentPath, CancellationToken token = default)
	{
		var originalContent = string.Empty;
		if (File.Exists (originalPath))
			originalContent = await File.ReadAllTextAsync (originalPath, token);
		
		// there is a reason why we are using uuid7 and it is because we can lexicographically sort the patches
		// With that information, we are going to do the following:
		// 1. Get all the patches that have been created for a file and sort them by the patch id
		// 2. Load the original content of the file
		// 3. Apply all the patches to the original content
		// 4. Generate the diff with the patch of the original content and the current content
		// 5. Return the new patch to be stored in the directory
		var previousPatchFiles = snapshot.GetPatches (currentPath); 

		foreach (string patchFile in previousPatchFiles) {
			//Log.Information ("Applying patch {Patch}", patchFile);
			var patchContent = await File.ReadAllTextAsync (patchFile, token);
			var oldPatches = _dmp.patch_fromText (patchContent);
			// from the docs: Applies a list of patches to text1. The first element of the return value is the newly
			// patched text. The second element is an array of true/false values indicating which of the patches were
			// successfully applied
			originalContent = _dmp.patch_apply (oldPatches, originalContent)[0] as string;
		}

		return originalContent ?? string.Empty;
	}

	async Task<string> CalculateDiff (string originalPath, string currentPath, CancellationToken token = default)
	{
		// get the previous content or an empty string if the file did not exist
		var currentContent = await File.ReadAllTextAsync (currentPath, token);
		var originalContent = await ApplyPatches (originalPath, currentPath, token);

		// create the diff vs the patched content, that way we have a diff with the very last diff
		var diffs = _dmp.diff_main (originalContent, currentContent);
		var patches = _dmp.patch_make (originalContent, diffs);
		var patchText = _dmp.patch_toText (patches);
		return patchText;
	}

	async Task ConsumeCreated (TextFileChangedEvent message, CancellationToken token = default)
	{
		Log.Information ("Create diff for new file {File}", message.Path);
		var currentContent = await File.ReadAllTextAsync (message.Path!, token);
		
		var directory = snapshot.GetDirectory (message.Path!);

		var diffs = _dmp.diff_main ("", currentContent);
		var patches = _dmp.patch_make ("", diffs);
		var patchText = _dmp.patch_toText (patches);
		
		//Log.Debug ("Diff for {Path} is {Diff}", message.Path, patchText);
		
		if (string.IsNullOrEmpty (patchText)) {
			//Log.Information ("Ignoring empty patch file for file {File}", message.Path);
			return;
		}

		var patchId = Uuid7.Guid();
		var patchPath = Path.Combine (directory, patchId.ToString());

		//Log.Information ("Writing diff to {Path}", patchPath);
		await File.WriteAllTextAsync (patchPath, patchText, token);
	}
	
	async Task ConsumeRemoved (TextFileChangedEvent message, CancellationToken token = default)
	{
		Log.Information ("Create diff for a removed file {File}", message.Path);
		
		// apply all the patches until now to the original file and the do a diff of a removal of the patched result
		// convert the path to a base64 string which we can use to store the diff in a directory
		var directory = snapshot.GetDirectory (message.Path!);
		var originalPath = snapshot.GetOriginalPath (message.Path!);

		var originalContent = await ApplyPatches (originalPath, message.Path!, token);
		var diffs = _dmp.diff_main (originalContent, string.Empty);
		var patches = _dmp.patch_make (originalContent, diffs);
		var patchText = _dmp.patch_toText (patches);

		//Log.Debug ("Diff for {Path} is {Diff}", message.Path, patchText);
		
		if (string.IsNullOrEmpty (patchText)) {
			//Log.Information ("Ignoring empty patch file for file {File}", message.Path);
			return;
		}

		var patchId = Uuid7.Guid();
		var patchPath = Path.Combine (directory, patchId.ToString());
		
		//Log.Information ("Writing diff to {Path}", patchPath);
		await File.WriteAllTextAsync (patchPath, patchText, token);
	}

	async Task ConsumeModified (TextFileChangedEvent message, CancellationToken token = default)
	{
		Log.Information ("Generating diff for {Event}", message);

		// convert the path to a base64 string which we can use to store the diff in a directory
		var directory = snapshot.GetDirectory (message.Path!);
		var originalPath = snapshot.GetOriginalPath (message.Path!);

		var patchText = await CalculateDiff (originalPath, message.Path!, token);
		//Log.Debug ("Diff for {Path} is {Diff}", message.Path, patchText);
		
		var patchId = Uuid7.Guid();
		var patchPath = Path.Combine (directory, patchId.ToString());
		
		//Log.Information ("Writing diff to {Path}", patchPath);
		await File.WriteAllTextAsync (patchPath, patchText, token);
	}
	
	Task ConsumeRenamed (TextFileChangedEvent message, CancellationToken token = default)
	{
		Log.Information ("Creating diff for renamed {Event}", message);
		return Task.FromResult (true);
	}

	public async Task ConsumeAsync (TextFileChangedEvent currentMessage, CancellationToken token = default)
	{
		Log.Information ("Consuming event {Event}", currentMessage);
		// ignore events without a path
		if (currentMessage.Path is null)
			return;

		// depending on the event type we will take different actions
		var task = currentMessage.Flags switch {
			TextFileChangedType.ItemRemoved => ConsumeRemoved (currentMessage, token),
			TextFileChangedType.ItemCreated => ConsumeCreated (currentMessage, token),
			TextFileChangedType.ItemModified => ConsumeModified (currentMessage, token),
			TextFileChangedType.ItemRenamed => ConsumeRenamed (currentMessage, token),
			_ => throw new InvalidOperationException ($"Unhandled event type {currentMessage.Flags}"),
		};
		await task;
	}

	public Task OnChannelClosedAsync (string channelName, CancellationToken token = default) => Task.CompletedTask;

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
