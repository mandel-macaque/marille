namespace Marille;

public abstract class Hub
{
    public bool TryRegister<T>(string topicName, IWorker<T> worker) => true;

    public bool TryRegister<T>(string topicName, Action<T> action) => true;

    public Task<T> Publish<T>(string topicName, T publishedEvent) => Task.FromResult<T>(default);

    public Task<T> Publish<T>(T publishedEvent) => Task.FromResult<T>(default);
}