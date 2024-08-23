using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Marille;

/// <summary>
/// Main implementation of the IHub interface. This class is responsible for managing the topics and the workers.
/// </summary>
public class Hub : IHub {
	readonly SemaphoreSlim semaphoreSlim;
	readonly Dictionary<string, Topic> topics = new();

	public Hub () : this (new (1))  { }
	internal Hub (SemaphoreSlim semaphore)
	{
		semaphoreSlim = semaphore;
	}

	async Task HandleConsumerError<T> (Task task, Channel<Message<T>> channel, Message<T> item)
		where T : struct
	{
		try {
			await task.ConfigureAwait (false);
		} catch (OperationCanceledException) {
			// retry on cancel
			await channel.Writer.WriteAsync (item).ConfigureAwait (false);
		} catch (Exception e) {
			// pump an error message back to the channel that will be dealt by the error worker
			var error = new Message<T> (item.Payload, e);
			await channel.Writer.WriteAsync (error).ConfigureAwait (false);
		}
	}

	void DeliverAtLeastOnceAsync<T> (Channel<Message<T>> channel, IWorker<T> [] workersArray, Message<T> item, 
		TimeSpan? timeout)
		where T : struct
	{
		Parallel.ForEachAsync  (workersArray, async (worker, _) => {
			CancellationToken token = default;
			if (timeout.HasValue) {
				var cts = new CancellationTokenSource ();
				cts.CancelAfter (timeout.Value);
				token = cts.Token;
			}
			var task = worker.ConsumeAsync (item.Payload, token);
			await HandleConsumerError (task, channel, item);
		});
	}

	Task DeliverAtLeastOnceSync<T> (Channel<Message<T>> channel, IWorker<T> [] workersArray, Message<T> item,
		TimeSpan? timeout)
		where T : struct
	{
		// we just need to execute all the provider workers with the same message and return the
		// task when all are done
		CancellationToken token = default;
		if (timeout.HasValue) {
			var cts = new CancellationTokenSource ();
			cts.CancelAfter (timeout.Value);
			token = cts.Token;
		}

		var tasks = new Task [workersArray.Length];
		for(var index = 0; index < workersArray.Length; index++) {
			var worker = workersArray [index];
			if (worker.UseBackgroundThread) {
				tasks [index] = Task.Run (async () => {
					await worker.ConsumeAsync (item.Payload, token).ConfigureAwait (false);
				}, token);
			} else {
				tasks [index] = worker.ConsumeAsync (item.Payload, token);
			}
		}
		return HandleConsumerError (Task.WhenAll (tasks), channel, item);
	}

	Task DeliverAtMostOnce<T> (Channel<Message<T>> channel, IWorker<T> [] workersArray, Message<T> item,
		TimeSpan? timeout)
		where T : struct
	{
		// we do know we are not empty, and in the AtMostOnce mode we will only use the first worker
		// present
		var worker = workersArray [0];
		CancellationToken token = default;
		if (timeout.HasValue) {
			var cts = new CancellationTokenSource ();
			cts.CancelAfter (timeout.Value);
			token = cts.Token;
		}

		var task = worker.UseBackgroundThread ? 
			Task.Run (async () => { 
				await worker.ConsumeAsync (item.Payload, token).ConfigureAwait (false);
			}, token) :
			worker.ConsumeAsync (item.Payload, token);
		return HandleConsumerError (task, channel, item); 
	}

	async Task ConsumeChannel<T> (TopicConfiguration configuration, Channel<Message<T>> ch, IErrorWorker<T> errorWorker, 
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
				if (item.Type == MessageType.Ack) 
					continue;
				if (item.IsError) {
					// do wait for the error worker to finish, we do not want to lose any error
					if (errorWorker.UseBackgroundThread)
						await Task.Run (async () => {
							await errorWorker.ConsumeAsync (
								item.Payload, item.Exception, cancellationToken).ConfigureAwait (false);
						}, cancellationToken);
					else
						await errorWorker.ConsumeAsync (
							item.Payload, item.Exception, cancellationToken).ConfigureAwait (false);
					continue;
				}
				switch (configuration.Mode) {
				case ChannelDeliveryMode.AtLeastOnceAsync:
					DeliverAtLeastOnceAsync (ch, workersArray, item, configuration.Timeout);
					break;
				case ChannelDeliveryMode.AtLeastOnceSync:
					// make the call 'sync' by not processing an item until we are done with the current one
					await DeliverAtLeastOnceSync (
						ch, workersArray, item, configuration.Timeout).ConfigureAwait (false);
					break;
				case ChannelDeliveryMode.AtMostOnceAsync:
					_ = Task.Run (async () => 
						await DeliverAtMostOnce (ch, workersArray, item, configuration.Timeout), cancellationToken);
					break;
				case ChannelDeliveryMode.AtMostOnceSync:
					// make the call 'sync' by not processing an item until we are done with the current one
					await DeliverAtMostOnce (ch, workersArray, item, configuration.Timeout).ConfigureAwait (false);
					break;
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
		topicInfo.ConsumerTask = ConsumeChannel (
			topicInfo.Configuration, topicInfo.Channel, topicInfo.ErrorWorker, workersCopy, completionSource, topicInfo.CancellationTokenSource.Token);
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

	public async Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration,
		IErrorWorker<T> errorWorker, params IWorker<T>[] initialWorkers) where T : struct
	{
		if (configuration.Mode == ChannelDeliveryMode.AtMostOnceAsync && initialWorkers.Length > 1)
			return false;

		// the topic might already have the channel, in that case, do nothing
		await semaphoreSlim.WaitAsync ().ConfigureAwait (false);
		try {
			if (!topics.TryGetValue (topicName, out Topic? topic)) {
				topic = new(topicName);
				topics [topicName] = topic;
			}

			if (!topic.TryCreateChannel (configuration, out var topicInfo, errorWorker, initialWorkers))
				return false;

			await StartConsuming (topicInfo).ConfigureAwait (false);
			return true;
		} finally {
			semaphoreSlim.Release ( );
		}
	}

	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration, IErrorWorker<T> errorWorker,
		IEnumerable<IWorker<T>> initialWorkers) where T : struct
		=> CreateAsync (topicName, configuration, errorWorker, initialWorkers.ToArray ());

	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration,
		Func<T, Exception, CancellationToken, Task> errorAction, params Func<T, CancellationToken, Task> [] actions) where T : struct
		=> CreateAsync (topicName, configuration, new LambdaErrorWorker<T> (errorAction), 
			actions.Select (a => new LambdaWorker<T> (a)));

	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration, IErrorWorker<T> errorWorker) where T : struct
		=> CreateAsync (topicName, configuration, errorWorker, Array.Empty<IWorker<T>> ());

	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration,
		Func<T, Exception, CancellationToken, Task> errorAction) where T : struct
		=> CreateAsync (topicName, configuration, new LambdaErrorWorker<T> (errorAction));
	
	public Task<bool> CreateAsync<T> (string topicName, TopicConfiguration configuration,
		Func<T, Exception, CancellationToken, Task> errorAction,
		Func<T, CancellationToken, Task> action) where T : struct
		=> CreateAsync (topicName, configuration, new LambdaErrorWorker<T> (errorAction),
			new LambdaWorker<T> (action));

	public async Task<bool> RegisterAsync<T> (string topicName, params IWorker<T>[] newWorkers) where T : struct
	{
		await semaphoreSlim.WaitAsync ().ConfigureAwait (false);
		try {
			// we only allow the client to register to an existing topic
			// in this API we will not create it, there are other APIs for that
			if (!TryGetChannel<T> (topicName, out _, out var topicInfo))
				return false;

			// do not allow to add more than one worker if we are in AtMostOnce mode.
			if (topicInfo.Configuration.Mode == ChannelDeliveryMode.AtMostOnceAsync && topicInfo.Workers.Count >= 1)
				return false;

			// we will have to stop consuming while we add the new worker
			// but we do not need to close the channel, the API will buffer
			StopConsuming<T> (topicName);
			topicInfo.Workers.AddRange (newWorkers);
			return await StartConsuming (topicInfo).ConfigureAwait (false);
		} finally {
			semaphoreSlim.Release ();
		}
	}

	public Task<bool> RegisterAsync<T> (string topicName, Func<T, CancellationToken, Task> action)  where T : struct
		=> RegisterAsync (topicName, new LambdaWorker<T> (action));

	public async ValueTask PublishAsync<T> (string topicName, T publishedEvent) where T : struct
	{
		await semaphoreSlim.WaitAsync ().ConfigureAwait (false);
		try {
			if (!TryGetChannel<T> (topicName, out _, out var topicInfo))
				throw new InvalidOperationException (
					$"Channel with topic {topicName} for event type {typeof (T)} not found");
			var message = new Message<T> (MessageType.Data, publishedEvent);
			await topicInfo.Channel.Writer.WriteAsync (message).ConfigureAwait (false);
		} finally {
			semaphoreSlim.Release ();
		}
	}

	public bool TryPublish<T> (string topicName, T publishedEvent) where T : struct
	{
		semaphoreSlim.Wait ();
		try {
			if (!TryGetChannel<T> (topicName, out _, out var topicInfo))
				throw new InvalidOperationException (
					$"Channel with topic {topicName} for event type {typeof (T)} not found");
			var message = new Message<T> (MessageType.Data, publishedEvent);
			return topicInfo.Channel.Writer.TryWrite (message);
		} finally {
			semaphoreSlim.Release ();
		}
	}

	public async Task CloseAllAsync ()
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
		await semaphoreSlim.WaitAsync ().ConfigureAwait (false);
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
		} finally {
			semaphoreSlim.Release ();
		}
	}

	public async Task<bool> CloseAsync<T> (string topicName) where T : struct
	{
		await semaphoreSlim.WaitAsync ().ConfigureAwait (false);
		try {
			// ensure that the channels does exist, if not, return false
			if (!TryGetChannel<T> (topicName, out var topic, out _))
				return false;

			// remove the channel, removing will call dispose
			await topic.RemoveChannel<T> ().ConfigureAwait (false);
			// clean the topic if needed
			if (topic.ChannelCount == 0) {
				topics.Remove (topicName);
			}
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

	public async ValueTask DisposeAsync ()
	{
		await DisposeAsyncCore ();
		GC.SuppressFinalize (this);
	}

	#endregion
}
