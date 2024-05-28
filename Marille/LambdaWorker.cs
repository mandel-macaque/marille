namespace Marille;

internal class LambdaWorker<T> (Func<T, CancellationToken,Task> lambda) : IWorker<T> where T : struct {
	public Task ConsumeAsync (T message, CancellationToken cancellationToken = default)
		=> lambda (message, cancellationToken);
}
