using System.Diagnostics;
using CoreServices;

namespace Marille.Workers;

public class EventFilterer(Hub hub) : IWorker<FSEvent> {
	Hub _hub = hub;
	
	public bool UseBackgroundThread => true;

	public async Task<string> GetFileMagicNumer (string path, CancellationToken token)
	{
		var startInfo = new ProcessStartInfo {
			UseShellExecute = false,
			FileName = "file",
			CreateNoWindow = true,
			RedirectStandardOutput = true,
			Arguments = $"-b {path}",
		};
		using var fileProcess = new Process();
		fileProcess.StartInfo = startInfo;
		fileProcess.Start();
		var fileType = (await fileProcess.StandardOutput.ReadToEndAsync(token)).Trim();
		return fileType;
	}
	public async Task ConsumeAsync (FSEvent message, CancellationToken token = default)
	{
		if (message.Path is null)
			return;

		// ignore directories
		if (message.Flags.HasFlag (FSEventStreamEventFlags.ItemIsDir))
			return;

		// ignore those events that are not a modification or creation of a file
		if (!message.Flags.HasFlag (FSEventStreamEventFlags.ItemModified)
		    || !message.Flags.HasFlag (FSEventStreamEventFlags.ItemCreated))
			return;

		// based on the path we can do several things, first if we do know the extension of the file
		// we will use that to decide what to do. Else we will use the file command to decide what to do.
		var extension = Path.GetExtension (message.Path);
		switch (extension) {
			case ".txt":
			case ".md":
			case ".cs":
			case ".json":
			case ".yml":
			case ".csharp":
			case ".gitignore":
			case ".config":
			case ".xml":
			case ".csproj":
			case ".h": 
			case ".m":
			case ".s":
			case ".t4":
			case ".plist":
				// post a new event as a file text changed
				await _hub.PublishAsync (nameof (FSMonitor), new TextFileChangedEvent (message));
				break;
			case ".png":
			case ".jpg":
			case ".lock":
			case ".lcl":
				return;
			default:
				// not efficient, useful for tests, if you have a lot of files you will have performance issues due
				// to the amount of times file will be called
				if (false) {
					var fileType = await GetFileMagicNumer (message.Path, token);
					Console.WriteLine (
						$"Unknown file extension {extension} for path {message.Path} magic number is '{fileType}'");
				} else {
					Console.WriteLine ($"Unknown file extension {extension} for path {message.Path}");
				}

				return;
		}
	}

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
