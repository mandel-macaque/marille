using CoreServices;
using Marille;

namespace FSEvents;

sealed class FSMonitor : FSEventStream {
	readonly Hub _hub;

	public FSMonitor (List<string> rootPaths, Hub hub, FSEventStreamCreateFlags createFlags)
		: base (rootPaths.ToArray (), TimeSpan.Zero, createFlags)
	{
		// keep a reference to the hub so that we can post the messages to it
		_hub = hub;
	}

	protected override void OnEvents (FSEvent [] events)
	{
		foreach (var evnt in events) {
			// publish to the hub the event, the workers will take care of it from different threads
			_hub.TryPublish (nameof (FSMonitor), evnt);
		}
	}
}
