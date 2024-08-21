namespace Marille;

internal class LambdaWorker<T> (Func<T, CancellationToken,Task> lambda) : IWorker<T> where T : struct {
	public async Task ConsumeAsync (T message, CancellationToken cancellationToken = default)
	{
		// await the lambda function so that we can wrap any exceptions in a Task
		await lambda (message, cancellationToken);
	}
}
