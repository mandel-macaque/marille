namespace Marille; 

public interface IHub {
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration,
		params IWorker<T> [] initialWorkers) where T : struct;
	public Task<bool> RegisterAsync<T> (string topicName, params IWorker<T> [] newWorkers) where T : struct;
	public ValueTask Publish<T> (string topicName, T publishedEvent) where T : struct;
	public Task CloseAllAsync ();

	public Task<bool> CloseAsync<T> (string topicName) where T : struct;
}
