using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Marille;

internal class Topic (string name) {
	readonly Dictionary<Type, (TopicConfiguration Configuration, object Channel)> channels = new();

	public string Name { get; } = name;

	public bool TryGetChannel<T> ([NotNullWhen (true)] out TopicInfo<T>? channel)
	{
		Type type = typeof (T);
		channel = null;
		if (!channels.TryGetValue (type, out var obj)) 
			return false;
		channel = new (obj.Configuration, (obj.Channel as Channel<T>)!);
		return true;
	}

	public Channel<T> CreateChannel<T> (TopicConfiguration configuration)
	{
		Type type = typeof (T);
		if (!channels.TryGetValue (type, out var obj)) {
			var ch = (configuration.Capacity is null) ? 
				Channel.CreateUnbounded<T> () : Channel.CreateBounded<T> (configuration.Capacity.Value);
			channels [type] = new (configuration, ch);
		}

		return (obj.Channel as Channel<T>)!;
	}

	public void CloseChannel<T> ()
	{
		// stop the channel from receiving events, this means that
		// eventually our dispatchers will complete
		if (TryGetChannel<T> (out var chInfo)) 
			chInfo.Channel.Writer.Complete ();
	}

	public bool ContainsChannel<T> ()
	{
		return channels.ContainsKey (typeof (T));
	}
}
