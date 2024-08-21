namespace Marille;

public interface IErrorWorker<in T> where T :  struct {

	public Task ConsumeAsync (T message, Exception exception, CancellationToken token = default);
}
