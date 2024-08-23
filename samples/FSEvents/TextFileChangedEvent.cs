using CoreServices;

namespace Marille;

public struct TextFileChangedEvent (FSEvent rawEvent) {
	public FSEvent RawEvent { get; } = rawEvent;
}
