using System.Threading.Channels;

namespace Marille;

internal abstract record TopicInfo (TopicConfiguration Configuration) : IDisposable, IAsyncDisposable {
	public CancellationTokenSource? CancellationTokenSource { get; set;  }
	public Task? ConsumerTask { get; set; }

	public abstract Task CloseChannel ();

	#region IDisposable Support

	protected virtual void Dispose (bool disposing)
	{
		if (disposing) {
			CancellationTokenSource?.Dispose ();
			ConsumerTask?.Dispose ();
		}
	}
	public void Dispose ()
	{
		Dispose (true);
		GC.SuppressFinalize (this);
	}

	protected virtual async ValueTask DisposeAsyncCore ()
	{
		if (CancellationTokenSource is not null) 
			await CastAndDispose (CancellationTokenSource);
		if (ConsumerTask is not null) 
			await CastAndDispose (ConsumerTask);

		return;

		static async ValueTask CastAndDispose (IDisposable resource)
		{
			if (resource is IAsyncDisposable resourceAsyncDisposable)
				await resourceAsyncDisposable.DisposeAsync ();
			else
				resource.Dispose ();
		}
	}

	public async ValueTask DisposeAsync ()
	{
		await DisposeAsyncCore ();
		GC.SuppressFinalize (this);
	}

	#endregion
}

internal record TopicInfo<T> (TopicConfiguration Configuration, Channel<Message<T>> Channel, IErrorWorker<T> ErrorWorker) 
	: TopicInfo (Configuration) where T : struct {

	public List<IWorker<T>> Workers { get; } = new();

	public TopicInfo (TopicConfiguration configuration, Channel<Message<T>> channel, IErrorWorker<T> errorWorker,
		params IWorker<T> [] workers) : this(configuration, channel, errorWorker)
	{
		ErrorWorker = errorWorker;
		Workers.AddRange (workers);
	}

	public override async Task CloseChannel ()
	{
		Channel.Writer.TryComplete ();
		if (ConsumerTask is not null) 
			await ConsumerTask.ConfigureAwait (false);
	}

	#region IDisposable Support

	protected override void Dispose (bool disposing)
	{
		Channel.Writer.TryComplete ();
		ConsumerTask?.Wait ();
		if (disposing) {
			// release all IWroker instances
			foreach (var worker in Workers) {
				worker.Dispose ();
			}
		}

		base.Dispose (disposing);
	}

	protected override async ValueTask DisposeAsyncCore ()
	{
		await CloseChannel ();
		await base.DisposeAsyncCore ();
		// release all IWorker instances
		foreach (var worker in Workers) {
			await worker.DisposeAsync ();
		}
	}

	#endregion
}
