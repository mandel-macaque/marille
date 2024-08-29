namespace Marille;

/// <summary>
/// Interface to be implemented by a worker that will take care of handling the errors from IWorker consumers in a
/// give channel.
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IErrorWorker<in T> : IDisposable, IAsyncDisposable where T : struct {

	/// <summary>
	/// Specifies if the worker should use a background thread to process the messages. If set to
	/// true the implementation of the worker should be thread safe.
	/// </summary>
	public bool UseBackgroundThread { get; }

	/// <summary>
	///
	/// </summary>
	/// <param name="message"></param>
	/// <param name="exception"></param>
	/// <param name="token"></param>
	/// <returns></returns>
	public Task ConsumeAsync (T message, Exception exception, CancellationToken token = default);
}
