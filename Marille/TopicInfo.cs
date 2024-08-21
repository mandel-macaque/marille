using System.Threading.Channels;

namespace Marille;

internal abstract record TopicInfo (TopicConfiguration Configuration){
	public CancellationTokenSource? CancellationTokenSource { get; set;  }
	public Task? ConsumerTask { get; set; }

	public abstract void CloseChannel ();
}

internal record TopicInfo<T> (TopicConfiguration Configuration, Channel<Message<T>> Channel, IErrorWorker<T> ErrorWorker) : TopicInfo (Configuration)
	where T : struct {
	public List<IWorker<T>> Workers { get; } = new();

	public TopicInfo (TopicConfiguration configuration, Channel<Message<T>> channel, IErrorWorker<T> errorWorker,
		params IWorker<T> [] workers) : this(configuration, channel, errorWorker)
	{
		ErrorWorker = errorWorker;
		Workers.AddRange (workers);
	}

	public override void CloseChannel ()
	{
		Channel.Writer.Complete ();
	}
}
