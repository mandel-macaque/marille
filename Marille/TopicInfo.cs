using System.Threading.Channels;

namespace Marille;

internal record TopicInfo {
	public Task? ConsumerTask { get; set; }
}

internal record TopicInfo<T> (TopicConfiguration Configuration, Channel<Message<T>> Channel) : TopicInfo
	where T : struct {
	public List<IWorker<T>> Workers { get; } = new();

	public TopicInfo (TopicConfiguration configuration, Channel<Message<T>> channel,
		params IWorker<T> [] workers) : this(configuration, channel)
	{
		Workers.AddRange (workers);
	}
}
