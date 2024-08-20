using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Marille;

internal class Topic (string name) {
	readonly Dictionary<Type, object> channels = new();

	public string Name { get; } = name;

	public bool TryGetChannel<T> ([NotNullWhen (true)] out TopicInfo<T>? channel) where T : struct
	{
		Type type = typeof (T);
		channel = null;
		if (!channels.TryGetValue (type, out var obj)) 
			return false;
		channel = obj as TopicInfo<T>;
		return channel is not null;
	}

	public TopicInfo<T> CreateChannel<T> (TopicConfiguration configuration, params IWorker<T>[] workers) where T : struct
	{
		Type type = typeof (T);
		if (!TryGetChannel<T> (out var obj)) {
			var ch = (configuration.Capacity is null) ? 
				Channel.CreateUnbounded<Message<T>> () : Channel.CreateBounded<Message<T>> (configuration.Capacity.Value);
			obj = new(configuration, ch, workers);
			channels[type] = obj; 
		}

		return obj;
	}

	public void CloseChannel<T> () where T : struct
	{
		// stop the channel from receiving events, this means that
		// eventually our dispatchers will complete
		if (!TryGetChannel<T> (out var chInfo))
			return;

		chInfo.Channel.Writer.Complete ();
		channels.Remove (typeof (T));
	}

	public bool ContainsChannel<T> ()
		=> channels.ContainsKey (typeof (T));

	public bool RemoveChannel<T> ()
		=> channels.Remove (typeof (T));
}
