using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Marille;

/// <summary>
/// 
/// </summary>
public abstract class Hub {
	readonly Random rnd = new();
	readonly Dictionary<string, Topic> topics = new();
	readonly Dictionary<(string Topic, Type Type), CancellationTokenSource> cancellationTokenSources = new();
	readonly Dictionary<(string Topic, Type type), List<object>> workers = new();
	
	public Channel<WorkerError> WorkersExceptions { get; } = Channel.CreateUnbounded<WorkerError> ();
	
	async Task ConsumeChannel<T> (TopicConfiguration configuration, Channel<T> ch, IWorker<T>[] workersArray, CancellationToken cancellationToken)
	{
		while (await ch.Reader.WaitToReadAsync (cancellationToken)) {
			while (ch.Reader.TryRead (out T? item)) {
				if (item is null)
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
						_ = worker.ConsumeAsync (item, token)
							.ContinueWith ((t) => { ch.Writer.WriteAsync (item); }, TaskContinuationOptions.OnlyOnCanceled) // TODO: max retries
							.ContinueWith ((t) => WorkersExceptions.Writer.WriteAsync (new WorkerError (typeof(T), worker, t.Exception)), 
								TaskContinuationOptions.OnlyOnFaulted);
					});
					break;
				default:
					// dumb algo atm, picking one at random, initialWorkers can have
					// state
					var index = (workers.Count == 0)? 0 : rnd.Next (workers.Count);
					var worker = workersArray [index];
					CancellationToken token = default;
					if (configuration.Timeout.HasValue) {
						var cts = new CancellationTokenSource ();
						cts.CancelAfter (configuration.Timeout.Value);
						token = cts.Token;
					}
					_ = worker.ConsumeAsync (item, token)
							.ContinueWith ((t) => { ch.Writer.WriteAsync (item);}, TaskContinuationOptions.OnlyOnCanceled)// TODO: max retries
							.ContinueWith ((t) => WorkersExceptions.Writer.WriteAsync (new WorkerError (typeof(T), worker, t.Exception)), 
								TaskContinuationOptions.OnlyOnFaulted);
					break;
				}
			}
		}
	}

	void StartConsuming<T> (string topicName, TopicConfiguration configuration, Channel<T> channel)
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

		// we have no interest in awaiting for this task
		_ = ConsumeChannel (configuration, channel, workersCopy, cancellationToken.Token);
	}

	void StopConsuming<T> (string topicName)
	{
		Type type = typeof (T);
		if (!cancellationTokenSources.TryGetValue ((topicName, type), out CancellationTokenSource? cancellationToken))
			return;

		cancellationToken.Cancel ();
		cancellationTokenSources.Remove ((topicName, type));
	}

	bool TryGetChannel<T> (string topicName, [NotNullWhen(true)] out TopicInfo<T>? ch)
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
	/// <param name="initialWorkers">Original set of IWoker<T> to be assigned the channel on creation.</param>
	/// <typeparam name="T">The event type to be used for the channel.</typeparam>
	/// <returns>true when the channel was created.</returns>
	public bool TryCreate<T> (string topicName, TopicConfiguration configuration,
		IEnumerable<IWorker<T>> initialWorkers)
	{
		// the topic might already have the channel, in that case, do nothing
		Type type = typeof (T);
		if (!topics.TryGetValue (topicName, out Topic? topic)) {
			topic = new();
			topics [topicName] = topic;
		}

		if (!workers.ContainsKey ((topicName, type))) {
			workers [(topicName, type)] = new List<object> (initialWorkers);
		}

		if (topic.TryGetChannel<T> (out _)) {
			return false;
		}
		var ch = topic.CreateChannel<T> (configuration);
		StartConsuming (topicName, configuration, ch);
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
	public bool TryCreate<T> (string topicName, TopicConfiguration configuration)
		=> TryCreate (topicName, configuration, Array.Empty<IWorker<T>> ());

	public bool TryCreateAndRegister<T> (string topicName) => false;
	
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
	public bool TryRegister<T> (string topicName, params IWorker<T>[] newWorkers)
	{
		// we only allow the client to register to an existing topic
		// in this API we will not create it, there are other APIs for that
		if (!TryGetChannel<T> (topicName, out var ch))
			return false;

		// we will have to stop consuming while we add the new worker
		// but we do not need to close the channel, the API will buffer
		StopConsuming<T> (topicName);
		workers [(topicName, typeof (T))].AddRange (newWorkers);
		StartConsuming (topicName, ch.Configuration, ch.Channel);
		return true;
	}

	public bool TryRegister<T> (string topicName, Func<T, CancellationToken, Task> action) =>
		TryRegister (topicName, new LambdaWorker<T> (action));

	public ValueTask Publish<T> (string topicName, T publishedEvent)
	{
		if (!TryGetChannel<T> (topicName, out var ch))
			throw new InvalidOperationException (
				$"Channel with topic {topicName} for event type {typeof(T)} not found");
		return ch.Channel.Writer.WriteAsync (publishedEvent);
	}
}
