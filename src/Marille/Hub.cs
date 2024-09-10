using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Marille;

/// <summary>
/// Main implementation of the IHub interface. This class is responsible for managing the topics and the workers.
/// </summary>
public class Hub : IHub {
	readonly SemaphoreSlim semaphoreSlim;
	readonly Dictionary<string, Topic> topics = new();
	readonly ILoggerFactory? loggerFactory;
	readonly ILogger<Hub>? logger;

	public Hub (ILoggerFactory? logging = null) : this (new (1), logging)  { }
	internal Hub (SemaphoreSlim semaphore, ILoggerFactory? logging = null)
	{
		semaphoreSlim = semaphore;
		loggerFactory = logging;
		logger = loggerFactory?.CreateLogger<Hub> ();
	}

	async Task HandleConsumerError<T> (string name, Task task, Channel<Message<T>> channel, Message<T> item)
		where T : struct
	{
		try {
			await task.ConfigureAwait (false);
		} catch (OperationCanceledException) {
			// retry on cancel
			logger?.LogTraceRetryOnCancel (name, typeof(T), item.Payload);
			await channel.Writer.WriteAsync (item).ConfigureAwait (false);
		} catch (Exception e) {
			// pump an error message back to the channel that will be dealt by the error worker
			logger?.LogTracePumpErrorMessage (name, typeof(T), item.Payload, e);
			var error = new Message<T> (item.Payload, e);
			await channel.Writer.WriteAsync (error).ConfigureAwait (false);
		}
	}

	void DeliverAtLeastOnceAsync<T> (string name, Channel<Message<T>> channel, IWorker<T> [] workersArray, Message<T> item, 
		TimeSpan? timeout)
		where T : struct
	{
		logger?.LogTraceDeliverAtLeastOnceAsync (item.Payload, name, typeof(T));
		Parallel.ForEachAsync  (workersArray, async (worker, _) => {
			CancellationToken token = default;
			if (timeout.HasValue) {
				logger?.LogTraceTimeoutCreation (timeout.Value);
				var cts = new CancellationTokenSource ();
				cts.CancelAfter (timeout.Value);
				token = cts.Token;
			}
			var task = worker.ConsumeAsync (item.Payload, token);
			await HandleConsumerError (name, task, channel, item);
		});
	}

	Task DeliverAtLeastOnceSync<T> (string name, Channel<Message<T>> channel, IWorker<T> [] workersArray, Message<T> item,
		TimeSpan? timeout)
		where T : struct
	{
		logger?.LogTraceDeliverAtLeastOnceSync (item.Payload, name, typeof(T));
		// we just need to execute all the provider workers with the same message and return the
		// task when all are done
		CancellationToken token = default;
		if (timeout.HasValue) {
			logger?.LogTraceTimeoutCreation (timeout.Value);
			var cts = new CancellationTokenSource ();
			cts.CancelAfter (timeout.Value);
			token = cts.Token;
		}

		var tasks = new Task [workersArray.Length];
		for(var index = 0; index < workersArray.Length; index++) {
			var worker = workersArray [index];
			_ = worker.TryGetUseBackgroundThread (out var useBackgroundThread);
			if (useBackgroundThread) {
				tasks [index] = Task.Run (async () => {
					await worker.ConsumeAsync (item.Payload, token).ConfigureAwait (false);
				}, token);
			} else {
				tasks [index] = worker.ConsumeAsync (item.Payload, token);
			}
		}
		return HandleConsumerError (name, Task.WhenAll (tasks), channel, item);
	}

	Task DeliverAtMostOnceAsync<T> (string name, Channel<Message<T>> channel, IWorker<T> [] workersArray, Message<T> item,
		TimeSpan? timeout)
		where T : struct
	{
		logger?.LogTraceDeliverAtMostOnceAsync (item.Payload, name, typeof(T));
		// we do know we are not empty, and in the AtMostOnce mode we will only use the first worker
		// present
		var worker = workersArray [0];
		CancellationToken token = default;
		if (timeout.HasValue) {
			logger?.LogTraceTimeoutCreation (timeout.Value);
			var cts = new CancellationTokenSource ();
			cts.CancelAfter (timeout.Value);
			token = cts.Token;
		}

		_ = worker.TryGetUseBackgroundThread (out var useBackgroundThread);
		var task = useBackgroundThread ? 
			Task.Run (async () => { 
				await worker.ConsumeAsync (item.Payload, token).ConfigureAwait (false);
			}, token) :
			worker.ConsumeAsync (item.Payload, token);
		return HandleConsumerError (name, task, channel, item); 
	}

	async Task ConsumeChannel<T> (string name, TopicConfiguration configuration, Channel<Message<T>> ch, IErrorWorker<T> errorWorker, 
		IWorker<T>[] workersArray, TaskCompletionSource<bool> completionSource, CancellationToken cancellationToken) where T : struct
	{
		// this is an important check, else the items will be consumer with no worker to receive them
		if (workersArray.Length == 0) {
			completionSource.SetResult (true);
			return;
		}

		// we want to set the completion source to true ONLY when we are consuming, that happens the first time
		// we have a WaitToReadAsync result. The or will ensure we do not call try set result more than once
		while (await ch.Reader.WaitToReadAsync (cancellationToken).ConfigureAwait (false) 
		       && (completionSource.Task.IsCompleted || completionSource.TrySetResult (true))) {
			while (ch.Reader.TryRead (out var item)) {
				// filter the ack message since it is only used to make sure that the task is indeed consuming
				if (item.Type == MessageType.Ack) {
					logger?.LogTraceAckReceived (name, typeof(T));		
					continue;
				}
				if (item.IsError) {
					logger?.LogTraceErrorReceived (name, typeof(T), item.Payload, item.Exception);
					// do wait for the error worker to finish, we do not want to lose any error. We are going to wrap
					// the error task in a try/catch to make sure that if the user did raise an exception, we do not
					// crash the whole consuming task. Sometimes java was right when adding exceptions to a method signature
					try {
						var _ = errorWorker.TryGetUseBackgroundThread (out var useBackgroundThread);
						if (useBackgroundThread)
							await Task.Run (async () => {
								await errorWorker.ConsumeAsync (
									item.Payload, item.Exception, cancellationToken).ConfigureAwait (false);
							}, cancellationToken);
						else
							await errorWorker.ConsumeAsync (
								item.Payload, item.Exception, cancellationToken).ConfigureAwait (false);
					} catch {
						// should we log the exception we are ignoring?
						logger?.LogErrorConsumerException (errorWorker.GetType (), item.Payload, item.Exception, name,
							typeof(T));
					}
					continue;
				}
				logger?.LogTraceConsumeMessage (name, typeof(T), item.Payload);
				switch (configuration.Mode) {
				case ChannelDeliveryMode.AtLeastOnceAsync:
					DeliverAtLeastOnceAsync (name, ch, workersArray, item, configuration.Timeout);
					break;
				case ChannelDeliveryMode.AtLeastOnceSync:
					// make the call 'sync' by not processing an item until we are done with the current one
					await DeliverAtLeastOnceSync (
						name, ch, workersArray, item, configuration.Timeout).ConfigureAwait (false);
					break;
				case ChannelDeliveryMode.AtMostOnceAsync:
					_ = Task.Run (async () => 
						await DeliverAtMostOnceAsync (name, ch, workersArray, item, configuration.Timeout), cancellationToken);
					break;
				case ChannelDeliveryMode.AtMostOnceSync:
					// make the call 'sync' by not processing an item until we are done with the current one
					await DeliverAtMostOnceAsync (name, ch, workersArray, item, configuration.Timeout).ConfigureAwait (false);
					break;
				}
			}
		}
		if (ch.Reader.Completion.IsCompleted) {
			// notify all the workers that no more messages are going to happen
			foreach (var worker in workersArray) {
				try {
					logger?.LogTraceCallOnChannelClose (worker.GetType (), name, typeof(T));
					await worker.OnChannelClosedAsync (name, cancellationToken).ConfigureAwait (false);
				} catch (Exception exception) {
					// OnChannelCloseAsync can throw! If that happens, we need to continue and
					// ignore the exception. We need to somehow communicat this.
					logger?.LogErrorOnChannelClose (worker.GetType (), exception, name, typeof(T));
				}
			}
		}
	}

	async Task<bool> StartConsuming<T> (TopicInfo<T> topicInfo)
		where T : struct
	{
		// we want to be able to cancel the thread that we are using to consume the
		// events for two different reasons:
		// 1. We are done with the work
		// 2. We want to add a new worker. Rather than risk a weird state
		//    in which we are running a thread and try to modify a collection, 
		//    we cancel the thread, use the channel as a buffer and do the changes
		if (topicInfo.CancellationTokenSource is not null)
			await topicInfo.CancellationTokenSource.CancelAsync ().ConfigureAwait (false);

		// create a new source for the topic, we cannot use the one that we used to cancel the previous one
		topicInfo.CancellationTokenSource = new ();
		var workersCopy = topicInfo.Workers.ToArray (); 

		// we have no interest in awaiting for this task, but we want to make sure it started. To do so
		// we create a TaskCompletionSource that will be set when the consume channel method is ready to consume
		var completionSource = new TaskCompletionSource<bool>();
		topicInfo.ConsumerTask = ConsumeChannel (topicInfo.TopicName, topicInfo.Configuration, topicInfo.Channel, 
			topicInfo.ErrorWorker, workersCopy, completionSource, topicInfo.CancellationTokenSource.Token);
		// send a message with a ack so that we can ensure we are indeed running
		_ = topicInfo.Channel.Writer.WriteAsync (new (MessageType.Ack), topicInfo.CancellationTokenSource.Token);
		return await completionSource.Task.ConfigureAwait (false);
	}

	void StopConsuming<T> (string topicName) where T : struct
	{
		if (!topics.TryGetValue (topicName, out var topic))
			return;
		if (!topic.TryGetChannel<T> (out var topicInfo))
			return;
		topicInfo.CancellationTokenSource?.Cancel ();
	}

	bool TryGetChannel<T> (string topicName, [NotNullWhen(true)] out Topic? topic, 
		[NotNullWhen(true)] out TopicInfo<T>? ch) where T : struct
	{
		topic = null;
		ch = null;
		if (!topics.TryGetValue (topicName, out topic)) {
			return false;
		}

		if (!topic.TryGetChannel (out ch)) {
			return false;
		}

		return true;
	}

	/// <inheritdoc />
	public async Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration,
		IErrorWorker<T> errorWorker, params IWorker<T>[] initialWorkers) where T : struct
	{
		logger?.LogCreateTopic (topicName, typeof(T));

		if (configuration.Mode == ChannelDeliveryMode.AtMostOnceAsync && initialWorkers.Length > 1) {
			logger?.LogWrongWorkerCount (topicName, typeof(T), configuration.Mode, initialWorkers.Length);	
			return false;
		}

		// the topic might already have the channel, in that case, do nothing
		await semaphoreSlim.WaitAsync ().ConfigureAwait (false);
		try {
			if (!topics.TryGetValue (topicName, out Topic? topic)) {
				topic = new(topicName);
				topics [topicName] = topic;
			}

			if (!topic.TryCreateChannel (configuration, out var topicInfo, errorWorker, initialWorkers)) {
				logger?.LogTopicAlreadyExists (topicName, typeof(T));	
				return false;
			}

			await StartConsuming (topicInfo).ConfigureAwait (false);
			return true;
		} finally {
			semaphoreSlim.Release ( );
		}
	}

	/// <inheritdoc />
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration, IErrorWorker<T> errorWorker,
		IEnumerable<IWorker<T>> initialWorkers) where T : struct
		=> CreateAsync (topicName, configuration, errorWorker, initialWorkers.ToArray ());

	/// <inheritdoc />
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration,
		Func<T, Exception, CancellationToken, Task> errorAction, params Func<T, CancellationToken, Task> [] actions) where T : struct
		=> CreateAsync (topicName, configuration, new LambdaErrorWorker<T> (errorAction), 
			actions.Select (a => new LambdaWorker<T> (a)));

	/// <inheritdoc />
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration, IErrorWorker<T> errorWorker) where T : struct
		=> CreateAsync (topicName, configuration, errorWorker, Array.Empty<IWorker<T>> ());

	/// <inheritdoc />
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration,
		Func<T, Exception, CancellationToken, Task> errorAction) where T : struct
		=> CreateAsync (topicName, configuration, new LambdaErrorWorker<T> (errorAction));
	
	/// <inheritdoc />
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration,
		Func<T, Exception, CancellationToken, Task> errorAction,
		Func<T, CancellationToken, Task> action) where T : struct
		=> CreateAsync (topicName, configuration, new LambdaErrorWorker<T> (errorAction),
			new LambdaWorker<T> (action));

	/// <inheritdoc />
	public async Task<bool> RegisterAsync<T> (string topicName, params IWorker<T>[] newWorkers) where T : struct
	{
		logger?.LogRegisterWorkers (topicName, typeof(T), newWorkers.Length);
		await semaphoreSlim.WaitAsync ().ConfigureAwait (false);
		try {
			// we only allow the client to register to an existing topic
			// in this API we will not create it, there are other APIs for that
			if (!TryGetChannel<T> (topicName, out _, out var topicInfo)) {
				logger?.LogRegisterErrorTopicDoesNotExist (topicName, typeof(T));
				return false;
			}

			// do not allow to add more than one worker if we are in AtMostOnce mode.
			if (topicInfo.Configuration.Mode == ChannelDeliveryMode.AtMostOnceAsync && topicInfo.Workers.Count >= 1) {
				logger?.LogRegisterErrorTooManyWorkers (topicName, typeof(T), topicInfo.Configuration.Mode, topicInfo.Workers.Count);	
				return false;
			}

			// we will have to stop consuming while we add the new worker
			// but we do not need to close the channel, the API will buffer
			StopConsuming<T> (topicName);
			topicInfo.Workers.AddRange (newWorkers);
			return await StartConsuming (topicInfo).ConfigureAwait (false);
		} finally {
			semaphoreSlim.Release ();
		}
	}

	/// <inheritdoc />
	public Task<bool> RegisterAsync<T> (string topicName, Func<T, CancellationToken, Task> action)  where T : struct
		=> RegisterAsync (topicName, new LambdaWorker<T> (action));

	/// <inheritdoc />
	public async ValueTask PublishAsync<T> (string topicName, T publishedEvent, 
		CancellationToken cancellationToken = default) where T : struct
	{
		await semaphoreSlim.WaitAsync (cancellationToken).ConfigureAwait (false);
		try {
			if (!TryGetChannel<T> (topicName, out _, out var topicInfo)) {
				logger?.LogPublishErrorTopicDoesNotExist (topicName, typeof(T));
				throw new InvalidOperationException (
					$"Channel with topic {topicName} for event type {typeof(T)} not found");
			}

			logger?.LogPublishAsyncEvent (topicName, publishedEvent);
			var message = new Message<T> (MessageType.Data, publishedEvent);
			await topicInfo.Channel.Writer.WriteAsync (message, cancellationToken).ConfigureAwait (false);
		} finally {
			semaphoreSlim.Release ();
		}
	}

	/// <inheritdoc />
	public async ValueTask PublishAsync<T> (string topicName, T? publishedEvent,
		CancellationToken cancellationToken = default) where T : struct
	{
		if (publishedEvent is not null)
			await PublishAsync (topicName, publishedEvent.Value, cancellationToken);
	}

	/// <inheritdoc />
	public bool TryPublish<T> (string topicName, T publishedEvent) where T : struct
	{
		semaphoreSlim.Wait ();
		try {
			if (!TryGetChannel<T> (topicName, out _, out var topicInfo)) {
				throw new InvalidOperationException (
					$"Channel with topic {topicName} for event type {typeof(T)} not found");
			}

			logger?.LogTryPublishEvent (topicName, publishedEvent);
			var message = new Message<T> (MessageType.Data, publishedEvent);
			return topicInfo.Channel.Writer.TryWrite (message);
		} finally {
			semaphoreSlim.Release ();
		}
	}

	/// <inheritdoc />
	public bool TryPublish<T> (string topicName, T? publishedEvent) where T : struct
	{
		return publishedEvent is not null
		       && TryPublish (topicName, publishedEvent.Value);
	}

	/// <inheritdoc />
	public async Task CloseAllAsync (CancellationToken cancellationToken = default)
	{
		// we are using this format to ensure that we have the right nullable types, if we where to use the following
		// 
		// var consumingTasks = cancellationTokenSources.Values
		//	.Select (x => x.ConsumeTask).Where (x => x is not null).ToArray ();
		// 
		// the compiler will force use to later do 
		//
		// `Task.WhenAll (consumingTasks!);` 
		//
		// suppressing the warning is ugly when we do know how to help the compiler ;)
		await semaphoreSlim.WaitAsync (cancellationToken).ConfigureAwait (false);
		try {
			var consumingTasks = from topic in topics.Values
				let tasks = topic.ConsumerTasks
				from task in tasks select task;

			// dispose all the topics, that will dispose all the channels and tasks should complete
			foreach (var info in topics.Values) {
				await info.DisposeAsync ().ConfigureAwait (false);
			}
			topics.Clear ();

			// the DisposeAsync should be closing the channels, but we will wait for the tasks to finish anyway
			await Task.WhenAll (consumingTasks).ConfigureAwait (false);
			logger?.LogCloseAllAsync ();
		} finally {
			semaphoreSlim.Release ();
		}
	}

	/// <inheritdoc />
	public async Task<bool> CloseAsync<T> (string topicName, CancellationToken cancellationToken = default) where T : struct
	{
		await semaphoreSlim.WaitAsync (cancellationToken).ConfigureAwait (false);
		try {
			// ensure that the channels does exist, if not, return false
			if (!TryGetChannel<T> (topicName, out var topic, out _)) {
				logger?.LogCloseErrorTopicDoesNotExist (topicName, typeof(T));	
				return false;
			}

			// remote the topic and clean its resources only when it has completed
			// consuming all events
			await using var topicInfo = topic.RemoveChannel<T> ();
			if (topicInfo is null)
				return false;
			
			await topicInfo.CloseChannel ().ConfigureAwait (false);
			if (topicInfo.ConsumerTask is not null) {
				await topicInfo.ConsumerTask;
			}

			// clean the topic if needed
			if (topic.ChannelCount == 0) {
				topics.Remove (topicName);
			}
			logger?.LogCloseAsync (topicName, typeof(T));
			return true;
		} finally {
			semaphoreSlim.Release ();
		}
	}
	
	#region IDisposable Support

	protected virtual void Dispose (bool disposing)
	{
		if (disposing) {
			semaphoreSlim.Dispose ();
			// close all topics, that will close all channels and tasks should complete
			foreach (var topic in topics.Values) {
				topic.Dispose ();
			}
		}
	}

	/// <inheritdoc />
	public void Dispose ()
	{
		Dispose (true);
		GC.SuppressFinalize (this);
	}

	protected virtual async ValueTask DisposeAsyncCore ()
	{
		semaphoreSlim.Dispose ();
		foreach (Topic topic in topics.Values) {
			await topic.DisposeAsync ();
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync ()
	{
		await DisposeAsyncCore ();
		GC.SuppressFinalize (this);
	}

	#endregion
}
