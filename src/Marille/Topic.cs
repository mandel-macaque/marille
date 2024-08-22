using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace Marille;

internal class Topic (string name) : IDisposable, IAsyncDisposable {
	readonly Dictionary<Type, TopicInfo> channels = new();

	public string Name { get; } = name;
	public int ChannelCount => channels.Count;

	public IEnumerable<Task> ConsumerTasks => from info in channels.Values
		where info.ConsumerTask is not null
		select info.ConsumerTask;

	public bool TryGetChannel<T> ([NotNullWhen (true)] out TopicInfo<T>? channel) where T : struct
	{
		Type type = typeof (T);
		channel = null;
		if (!channels.TryGetValue (type, out var obj)) 
			return false;
		channel = obj as TopicInfo<T>;
		return channel is not null;
	}

	public TopicInfo<T> CreateChannel<T> (TopicConfiguration configuration, IErrorWorker<T> errorWorker,
		params IWorker<T>[] workers) where T : struct
	{
		Type type = typeof (T);
		if (!TryGetChannel<T> (out var obj)) {
			var ch = (configuration.Capacity is null) ? 
				Channel.CreateUnbounded<Message<T>> () : 
				Channel.CreateBounded<Message<T>> (configuration.Capacity.Value);
			obj = new(configuration, ch, errorWorker, workers);
			channels[type] = obj; 
		}

		return obj;
	}

	public async Task RemoveChannel<T> () where T : struct
	{
		if (!TryGetChannel<T> (out var topicInfo))
			return;

		await topicInfo.DisposeAsync ();
		channels.Remove (typeof (T));
	}

	#region IDisposable Support

	protected virtual void Dispose (bool disposing)
	{
		if (disposing) {
			foreach (var topicInfo in channels.Values) {
				topicInfo.Dispose ();
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
		foreach (var topicInfo in channels.Values) {
			await topicInfo.DisposeAsync ();
		}
	}

	public async ValueTask DisposeAsync ()
	{
		await DisposeAsyncCore ();
		GC.SuppressFinalize (this);
	}

	#endregion
}
