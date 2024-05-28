using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Marille;

internal class Topic (string name) {
	readonly Dictionary<Type, (TopicConfiguration Configuration, object Channel)> channels = new();

	public string Name { get; } = name;

	public bool TryGetChannel<T> ([NotNullWhen (true)] out TopicInfo<T>? channel) where T : struct
	{
		Type type = typeof (T);
		channel = null;
		if (!channels.TryGetValue (type, out var obj)) 
			return false;
		channel = new (obj.Configuration, (obj.Channel as Channel<Message<T>>)!);
		return true;
	}

	public Channel<Message<T>> CreateChannel<T> (TopicConfiguration configuration) where T : struct
	{
		Type type = typeof (T);
		if (!channels.TryGetValue (type, out var obj)) {
			var ch = (configuration.Capacity is null) ? 
				Channel.CreateUnbounded<Message<T>> () : Channel.CreateBounded<Message<T>> (configuration.Capacity.Value);
			obj = new (configuration, ch);
			channels[type] = obj; 
		}

		return (obj.Channel as Channel<Message<T>>)!;
	}

	public void CloseChannel<T> () where T : struct
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
