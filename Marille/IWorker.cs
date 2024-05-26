namespace Marille;

/// <summary>
/// Represents a worker that will be able to consume messages from a given topic channel.
/// </summary>
/// <typeparam name="T">The type of events consumed by the worker.</typeparam>
public interface IWorker<in T> {

	/// <summary>
	/// Method that will be executed for every event in the channel that has been assigned
	/// to the worker instance.
	/// </summary>
	/// <param name="message">The messages from the channel assigned to be processed by the worker instance.</param>
	/// <param name="token">Calculation toke provided to the worker. This cancellation token should be respected.</param>
	/// <returns></returns>
	public Task ConsumeAsync (T message, CancellationToken token = default);
}
