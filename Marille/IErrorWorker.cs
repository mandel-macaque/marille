namespace Marille;

public interface IErrorWorker<in T> : IDisposable, IAsyncDisposable where T : struct {

	public Task ConsumeAsync (T message, Exception exception, CancellationToken token = default);
}
