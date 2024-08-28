using HeyRed.Mime;
using Serilog;

namespace Marille.FileSystem.Workers;

public abstract class EventFilterer<T> : IWorker<T> where T: struct {

	#region Helpers

	protected bool IsValidMimeType (string path)
	{
		var mime = MimeGuesser.GuessFileType (path);
		Log.Information ("Mime type for {Path} is {Mime}", path, mime.MimeType);
		return mime.MimeType switch {
			"application/json" => true,
			var str when str.StartsWith ("text/") => true,
			_ => false,
		};
	}

	#endregion

	#region IWorker

	public abstract bool UseBackgroundThread { get; }
	public abstract Task ConsumeAsync (T currentMessage, CancellationToken token = default);
	public abstract Task OnChannelClosedAsync (string channelName, CancellationToken token = default);

	#endregion

	protected virtual void Dispose(bool disposing) { }

	public void Dispose()
	{
		Dispose (true);
		GC.SuppressFinalize (this);
	}

	protected virtual ValueTask DisposeAsyncCore () => ValueTask.CompletedTask;

	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCore ();
		GC.SuppressFinalize (this);
	}
}
