namespace Marille;

internal class LambdaWorker<T> (Func<T, CancellationToken,Task> lambda) : IWorker<T> {
	public Task ConsumeAsync (T message, CancellationToken cancellationToken = default)
		=> lambda (message, cancellationToken);
}
