namespace Marille; 

/// <summary>
/// Interface that defines the operations that can be performed on a Hub. A Hub is a collection of channels that
/// can be used to deliver messages to workers. The Hub is responsible for creating channels, adding workers to them,
/// and delivering messages to the workers.
/// </summary>
public interface IHub : IDisposable, IAsyncDisposable {
	
	/// <summary>
	/// Attempts to create a new channel for the given topic name using the provided configuration. Channels cannot
	/// be created more than once, in case the channel already exists this method returns false;
	///
	/// The provided workers will be added to the pool of workers that will be consuming events.
	/// </summary>
	/// <param name="topicName">The topic used to identify the channel. The same topic can have channels for different
	/// types of events, but the combination (topicName, eventType) has to be unique.</param>
	/// <param name="configuration">The configuration to use for the channel creation.</param>
	/// <param name="errorWorker">Worker that will receive any error from faulty worker executions.</param>
	/// <param name="initialWorkers">Original set of IWorker&lt;T&gt; to be assigned the channel on creation.</param>
	/// <typeparam name="T">The event type to be used for the channel.</typeparam>
	/// <returns>true when the channel was created.</returns>
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration, IErrorWorker<T> errorWorker,
		params IWorker<T> [] initialWorkers) where T : struct;

	/// <summary>
	/// Attempts to create a new channel for the given topic name using the provided configuration. Channels cannot
	/// be created more than once, in case the channel already exists this method returns false;
	///
	/// The provided workers will be added to the pool of workers that will be consuming events.
	/// </summary>
	/// <param name="topicName">The topic used to identify the channel. The same topic can have channels for different
	/// types of events, but the combination (topicName, eventType) has to be unique.</param>
	/// <param name="configuration">The configuration to use for the channel creation.</param>
	/// <param name="errorWorker">Worker that will receive any error from faulty worker executions.</param>
	/// <param name="initialWorkers">Original set of IWorker&lt;T&gt; to be assigned the channel on creation.</param>
	/// <typeparam name="T">The event type to be used for the channel.</typeparam>
	/// <returns>true when the channel was created.</returns>
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration, IErrorWorker<T> errorWorker,
		IEnumerable<IWorker<T>> initialWorkers) where T : struct;

	/// <summary>
	/// Attempts to create a new channel for the given topic name using the provided configuration. Channels cannot
	/// be created more than once, in case the channel already exists this method returns false;
	/// </summary>
	/// <param name="topicName">The topic used to identify the channel. The same topic can have channels for different
	/// types of events, but the combination (topicName, eventType) has to be unique.</param>
	/// <param name="configuration">The configuration to use for the channel creation.</param>
	/// <param name="errorAction">Worker that will receive any error from faulty worker executions.</param>
	/// <param name="actions">A set of functions that will be executed when an item is delivered.</param>
	/// <typeparam name="T">The event type to be used for the channel.</typeparam>
	/// <returns>true when the channel was created.</returns>
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration, 
		Func<T, Exception, CancellationToken, Task> errorAction,
		params Func<T, CancellationToken, Task> [] actions) where T : struct;

	/// <summary>
	/// Attempts to create a new channel for the given topic name using the provided configuration. Channels cannot
	/// be created more than once, in case the channel already exists this method returns false;
	/// </summary>
	/// <param name="topicName">The topic used to identify the channel. The same topic can have channels for different
	/// types of events, but the combination (topicName, eventType) has to be unique.</param>
	/// <param name="configuration">The configuration to use for the channel creation.</param>
	/// <param name="errorAction">Worker that will receive any error from faulty worker executions.</param>
	/// <param name="action">The function that will be executed when a message is delivered.</param>
	/// <typeparam name="T">The event type to be used for the channel.</typeparam>
	/// <returns>true when the channel was created.</returns>
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration, 
		Func<T, Exception, CancellationToken, Task> errorAction,
		Func<T, CancellationToken, Task> action) where T : struct;

	/// <summary>
	/// Attempts to create a new channel for the given topic name using the provided configuration. Channels cannot
	/// be created more than once, in case the channel already exists this method returns false;
	///
	/// No workers will be assigned to the channel upon creation.
	/// </summary>
	/// <param name="topicName">The topic used to identify the channel. The same topic can have channels for different
	/// types of events, but the combination (topicName, eventType) has to be unique.</param>
	/// <param name="configuration">The configuration to use for the channel creation.</param>
	/// <param name="errorWorker">Worker that will receive any error from faulty worker executions.</param>
	/// <typeparam name="T">The event type to be used for the channel.</typeparam>
	/// <returns>true when the channel was created.</returns>
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration,
		IErrorWorker<T> errorWorker) where T : struct;

	/// <summary>
	/// Attempts to create a new channel for the given topic name using the provided configuration. Channels cannot
	/// be created more than once, in case the channel already exists this method returns false;
	///
	/// No workers will be assigned to the channel upon creation.
	/// </summary>
	/// <param name="topicName">The topic used to identify the channel. The same topic can have channels for different
	/// types of events, but the combination (topicName, eventType) has to be unique.</param>
	/// <param name="configuration">The configuration to use for the channel creation.</param>
	/// <param name="errorAction">Worker that will receive any error from faulty worker executions.</param>
	/// <typeparam name="T">The event type to be used for the channel.</typeparam>
	/// <returns>true when the channel was created.</returns>
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration,
		Func<T, Exception, CancellationToken, Task> errorAction) where T : struct;

	/// <summary>
	/// Attempts to register new workers to consume messages for the given topic.
	/// </summary>
	/// <param name="topicName">The topic name that will deliver messages to the worker.</param>
	/// <param name="newWorkers">The worker to add to the pool.</param>
	/// <typeparam name="T">The type of messages of the topic.</typeparam>
	/// <returns>true if the worker could be added.</returns>
	/// <remarks>Workers can be added to channels that are already being processed. The Hub will pause the consumtion
	/// of the messages while it adds the worker and will resume the processing after. Producer can be sending
	/// messages while this operation takes place because messages will be buffered by the channel.</remarks>
	public Task<bool> RegisterAsync<T> (string topicName, params IWorker<T> [] newWorkers) where T : struct;

	/// <summary>
	/// Adds a new lambda based worker to the topic allowing it to consume messages.
	/// </summary>
	/// <param name="topicName">The topic name that will deliver messages to the worker.</param>
	/// <param name="action">The lambda that will be executed per messages received.</param>
	/// <typeparam name="T">The type of messages of the topic.</typeparam>
	/// <returns>true if the worker could be added.</returns>
	/// <remarks>Workers can be added to channels that are already being processed. The Hub will pause the consumtion
	/// of the messages while it adds the worker and will resume the processing after. Producer can be sending
	/// messages while this operation takes place because messages will be buffered by the channel.</remarks>
	public Task<bool> RegisterAsync<T> (string topicName, Func<T, CancellationToken, Task> action) where T : struct;
	
	/// <summary>
	/// Allows to publish a message in a given topic. The message will be added to a channel and will be
	/// consumed by any worker that might have been added.
	/// </summary>
	/// <param name="topicName">The topic name that will deliver messages to the worker.</param>
	/// <param name="publishedEvent">The message to be publish in the topic.</param>
	/// <typeparam name="T">The type of messages of the topic.</typeparam>
	/// <returns>true of the message was delivered to the topic.</returns>
	/// <exception cref="InvalidOperationException">Thrown if no topic can be found with the provided
	/// (topicName, messageType) combination.</exception>
	public ValueTask PublishAsync<T> (string topicName, T publishedEvent) where T : struct;

	/// <summary>
	/// Allows to publish a message in a given topic. The message will be added to a channel and will be
	/// consumed by any worker that might have been added.
	/// </summary>
	/// <param name="topicName">The topic name that will deliver messages to the worker.</param>
	/// <param name="publishedEvent">The message to be publish in the topic.</param>
	/// <typeparam name="T">The type of messages of the topic.</typeparam>
	/// <returns>true of the message was delivered to the topic.</returns>
	/// <exception cref="InvalidOperationException">Thrown if no topic can be found with the provided
	/// (topicName, messageType) combination.</exception>
	public ValueTask PublishAsync<T> (string topicName, T? publishedEvent) where T : struct;

	/// <summary>
	/// Allows to publish a message in the given topic. The method will fail on bounded channels depending on the
	/// semantics that were used to create the channel.
	/// </summary>
	/// <param name="topicName">The topic name that will deliver messages to the worker.</param>
	/// <param name="publishedEvent">The message to be publish in the topic.</param>
	/// <typeparam name="T">The type of messages of the topic.</typeparam>
	/// <returns>true of the message was delivered to the topic.</returns>
	/// <exception cref="InvalidOperationException">Thrown if no topic can be found with the provided
	/// (topicName, messageType) combination.</exception>
	/// <returns>true if the event could be publish.</returns>
	public bool TryPublish<T> (string topicName, T publishedEvent) where T : struct;

	/// <summary>
	/// Allows to publish a message in the given topic. The method will fail on bounded channels depending on the
	/// semantics that were used to create the channel.
	/// </summary>
	/// <param name="topicName">The topic name that will deliver messages to the worker.</param>
	/// <param name="publishedEvent">The message to be publish in the topic.</param>
	/// <typeparam name="T">The type of messages of the topic.</typeparam>
	/// <returns>true of the message was delivered to the topic.</returns>
	/// <exception cref="InvalidOperationException">Thrown if no topic can be found with the provided
	/// (topicName, messageType) combination.</exception>
	/// <returns>true if the event could be publish.</returns>
	public bool TryPublish<T> (string topicName, T? publishedEvent) where T : struct;

	/// <summary>
	/// Cancel all channels in the Hub and return a task that will be completed once all the channels have been flushed.
	/// </summary>
	/// <returns>A task that will be completed once ALL the channels have been flushed.</returns>
	public Task CloseAllAsync ();

	/// <summary>
	/// Cancels the channel in the Hub with the given token and returns a task that will be completed once the channel
	/// has been flushed. 
	/// </summary>
	/// <param name="topicName">The name of the topic to cancel.</param>
	/// <typeparam name="T">The type of events of the topic.</typeparam>
	/// <returns>A task that will be completed once the channel has been flushed.</returns>
	public Task<bool> CloseAsync<T> (string topicName) where T : struct;
}
