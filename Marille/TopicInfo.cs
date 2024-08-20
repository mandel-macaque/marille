using System.Threading.Channels;

namespace Marille;

internal record TopicInfo<T> (TopicConfiguration Configuration, Channel<Message<T>> Channel)
	where T : struct {
	public List<IWorker<T>> Workers { get; } = new();

	public TopicInfo (TopicConfiguration configuration, Channel<Message<T>> channel,
		params IWorker<T> [] workers) : this(configuration, channel)
	{
		Workers.AddRange (workers);
	}
}
