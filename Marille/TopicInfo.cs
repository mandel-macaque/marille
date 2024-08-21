using System.Threading.Channels;

namespace Marille;

internal abstract record TopicInfo (TopicConfiguration Configuration){
	public CancellationTokenSource? CancellationTokenSource { get; set;  }
	public Task? ConsumerTask { get; set; }

	public abstract void CloseChannel ();
}

internal record TopicInfo<T> (TopicConfiguration Configuration, Channel<Message<T>> Channel) : TopicInfo (Configuration)
	where T : struct {
	public List<IWorker<T>> Workers { get; } = new();

	public TopicInfo (TopicConfiguration configuration, Channel<Message<T>> channel,
		params IWorker<T> [] workers) : this(configuration, channel)
	{
		Workers.AddRange (workers);
	}

	public override void CloseChannel ()
	{
		Channel.Writer.Complete ();
	}
}
