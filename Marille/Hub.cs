using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Marille;

/// <summary>
/// 
/// </summary>
public class Hub {
	readonly Dictionary<string, Topic> topics = new();
	readonly Dictionary<(string Topic, Type Type), CancellationTokenSource> cancellationTokenSources = new();
	readonly Dictionary<(string Topic, Type type), List<object>> workers = new();
	
	public Channel<WorkerError> WorkersExceptions { get; } = Channel.CreateUnbounded<WorkerError> ();
	
	async Task ConsumeChannel<T> (TopicConfiguration configuration, Channel<Message<T>> ch, IWorker<T>[] workersArray, 
		TaskCompletionSource<bool> completionSource, CancellationToken cancellationToken) where T : struct
	{
		// this is an important check, else the items will be consumer with no worker to receive them
		if (workersArray.Length == 0) {
			completionSource.SetResult (true);
			return;
		}

		// we want to set the completion source to true ONLY when we are consuming, that happens the first time
		// we have a WaitToReadAsync result. The or will ensure we do not call try set result more than once
		while (await ch.Reader.WaitToReadAsync (cancellationToken) 
		       && (completionSource.Task.IsCompleted || completionSource.TrySetResult (true))) {
			while (ch.Reader.TryRead (out var item)) {
				// filter the ack message since it is only used to make sure that the task is indeed consuming
				if (item.Type == MessageType.Ack) 
					continue;
				switch (configuration.Mode) {
				case ChannelDeliveryMode.AtLeastOnce:
					Parallel.ForEach (workersArray, worker => {
						CancellationToken token = default;
						if (configuration.Timeout.HasValue) {
							var cts = new CancellationTokenSource ();
							cts.CancelAfter (configuration.Timeout.Value);
							token = cts.Token;
						}
						_ = worker.ConsumeAsync (item.Payload, token)
							.ContinueWith ((t) => { ch.Writer.WriteAsync (item); }, TaskContinuationOptions.OnlyOnCanceled) // TODO: max retries
							.ContinueWith ((t) => WorkersExceptions.Writer.WriteAsync (new WorkerError (typeof(T), worker, t.Exception)), 
								TaskContinuationOptions.OnlyOnFaulted);
					});
					break;
				default:
					// we do know we are not empty, and in the AtMostOnce mode we will only use the first worker
					// present
					var worker = workersArray [0];
					CancellationToken token = default;
					if (configuration.Timeout.HasValue) {
						var cts = new CancellationTokenSource ();
						cts.CancelAfter (configuration.Timeout.Value);
						token = cts.Token;
					}
					_ = worker.ConsumeAsync (item.Payload, token)
						.ContinueWith ((t) => { ch.Writer.WriteAsync (item); },
							TaskContinuationOptions.OnlyOnCanceled) // TODO: max retries
						.ContinueWith (
							(t) => WorkersExceptions.Writer.WriteAsync (new WorkerError (typeof (T), worker,
								t.Exception)),
							TaskContinuationOptions.OnlyOnFaulted);
					break;
				}
			}
		}
	}

	Task<bool> StartConsuming<T> (string topicName, TopicConfiguration configuration, Channel<Message<T>> channel)
		where T : struct
	{
		Type type = typeof (T);
		// we want to be able to cancel the thread that we are using to consume the
		// events for two different reasons:
		// 1. We are done with the work
		// 2. We want to add a new worker. Rather than risk a weird state
		//    in which we are running a thread and try to modify a collection, 
		//    we cancel the thread, use the channel as a buffer and do the changes
		var cancellationToken = new CancellationTokenSource ();
		cancellationTokenSources [(topicName, type)] = cancellationToken;
		var workersCopy = workers[(topicName, type)].Select (x => (IWorker<T>)x).ToArray ();

		// we have no interest in awaiting for this task, but we want to make sure it started. To do so
		// we create a TaskCompletionSource that will be set when the consume channel method is ready to consume
		var completionSource = new TaskCompletionSource<bool>();
		_ = ConsumeChannel (configuration, channel, workersCopy, completionSource, cancellationToken.Token);
		// send a message with a ack so that we can ensure we are indeed running
		_ = channel.Writer.WriteAsync (new Message<T> (MessageType.Ack));
		return completionSource.Task;
	}

	void StopConsuming<T> (string topicName) where T : struct
	{
		Type type = typeof (T);
		if (!cancellationTokenSources.TryGetValue ((topicName, type), out CancellationTokenSource? cancellationToken))
			return;

		cancellationToken.Cancel ();
		cancellationTokenSources.Remove ((topicName, type));
	}

	bool TryGetChannel<T> (string topicName, [NotNullWhen(true)] out TopicInfo<T>? ch) where T : struct
	{
		ch = null;
		if (!topics.TryGetValue (topicName, out Topic? topic)) {
			return false;
		}

		if (!topic.TryGetChannel (out ch)) {
			return false;
		}

		return true;
	}

	/// <summary>
	/// Attempts to create a new channel for the given topic name using the provided configuration. Channels cannot
	/// be created more than once, in case the channel already exists this method returns false;
	///
	/// The provided workers will be added to the pool of workers that will be consuming events.
	/// </summary>
	/// <param name="topicName">The topic used to identify the channel. The same topic can have channels for different
	/// types of events, but the combination (topicName, eventType) has to be unique.</param>
	/// <param name="configuration">The configuration to use for the channel creation.</param>
	/// <param name="initialWorkers">Original set of IWorker&lt;T&gt; to be assigned the channel on creation.</param>
	/// <typeparam name="T">The event type to be used for the channel.</typeparam>
	/// <returns>true when the channel was created.</returns>
	public async Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration,
		IEnumerable<IWorker<T>> initialWorkers) where T : struct
	{
		if (configuration.Mode == ChannelDeliveryMode.AtMostOnce && initialWorkers.Count () > 1)
			return false;

		// the topic might already have the channel, in that case, do nothing
		Type type = typeof (T);
		if (!topics.TryGetValue (topicName, out Topic? topic)) {
			topic = new(topicName);
			topics [topicName] = topic;
		}

		if (!workers.ContainsKey ((topicName, type))) {
			workers [(topicName, type)] = new(initialWorkers);
		}

		if (topic.TryGetChannel<T> (out _)) {
			return false;
		}
		var ch = topic.CreateChannel<T> (configuration);
		await StartConsuming (topicName, configuration, ch);
		return true;
	}

	/// <summary>
	/// Attempts to create a new channel for the given topic name using the provided configuration. Channels cannot
	/// be created more than once, in case the channel already exists this method returns false;
	///
	/// No workes will be assigned to the channel upon creation.
	/// </summary>
	/// <param name="topicName">The topic used to identify the channel. The same topic can have channels for different
	/// types of events, but the combination (topicName, eventType) has to be unique.</param>
	/// <param name="configuration">The configuration to use for the channel creation.</param>
	/// <typeparam name="T">The event type to be used for the channel.</typeparam>
	/// <returns>true when the channel was created.</returns>
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration) where T : struct
		=> CreateAsync (topicName, configuration, Array.Empty<IWorker<T>> ());

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
	public Task<bool> RegisterAsync<T> (string topicName, params IWorker<T>[] newWorkers) where T : struct
	{
		var type = typeof (T);
		// we only allow the client to register to an existing topic
		// in this API we will not create it, there are other APIs for that
		if (!TryGetChannel<T> (topicName, out var ch))
			return Task.FromResult(false);

		// do not allow to add more than one worker ig we are in AtMostOnce mode.
		if (ch.Configuration.Mode == ChannelDeliveryMode.AtMostOnce && workers [(topicName, type)].Count >= 1)
			return Task.FromResult (false);

		// we will have to stop consuming while we add the new worker
		// but we do not need to close the channel, the API will buffer
		StopConsuming<T> (topicName);
		workers [(topicName, type)].AddRange (newWorkers);
		return StartConsuming (topicName, ch.Configuration, ch.Channel);
	}

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
	public Task<bool> RegisterAsync<T> (string topicName, Func<T, CancellationToken, Task> action)  where T : struct
		=> RegisterAsync (topicName, new LambdaWorker<T> (action));

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
	public ValueTask Publish<T> (string topicName, T publishedEvent) where T : struct
	{
		if (!TryGetChannel<T> (topicName, out var ch))
			throw new InvalidOperationException (
				$"Channel with topic {topicName} for event type {typeof(T)} not found");
		var message = new Message<T> (MessageType.Data, publishedEvent);
		return ch.Channel.Writer.WriteAsync (message);
	}
}
