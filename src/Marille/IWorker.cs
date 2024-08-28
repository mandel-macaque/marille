namespace Marille;

/// <summary>
/// Represents a worker that will be able to consume messages from a given topic channel.
/// </summary>
/// <typeparam name="T">The type of events consumed by the worker.</typeparam>
public interface IWorker<in T> : IDisposable, IAsyncDisposable where T :  struct {

	/// <summary>
	/// Specifies if the worker should use a background thread to process the messages. If set to
	/// true the implementation of the worker should be thread safe.
	/// </summary>
	public bool UseBackgroundThread { get; }

	/// <summary>
	/// Method that will be executed for every event in the channel that has been assigned
	/// to the worker instance.
	/// </summary>
	/// <param name="message">The messages from the channel assigned to be processed by the worker instance.</param>
	/// <param name="token">Calculation toke provided to the worker. This cancellation token should be respected.</param>
	/// <returns>The task to be awaited.</returns>
	public Task ConsumeAsync (T message, CancellationToken token = default);

	/// <summary>
	/// Method to be executed when the channel has been closed. This allows the worker to perform any cleanup.
	/// </summary>
	/// <param name="channelName">The channel name that has been closed.</param>
	/// <param name="token">Calculation toke provided to the worker. This cancellation token should be respected.</param>
	/// <returns>The task to be awaited.</returns>
	public Task OnChannelClosedAsync (string channelName, CancellationToken token = default);
}
