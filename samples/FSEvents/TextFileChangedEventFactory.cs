using CoreServices;
using Marille.FileSystem.Events;
using Serilog;

namespace FSEvents;

/// <summary>
///
/// </summary>
public static class TextFileChangedEventFactory {

	public static TextFileChangedEvent? FromFSEvent (FSEvent rawEvent)
	{
		// we can have ore than one flag so we need to be smart about it
		var flag = TextFileChangedType.Unknown;

		if (rawEvent.Flags.HasFlag (FSEventStreamEventFlags.ItemRemoved)) {
			flag = TextFileChangedType.ItemRemoved;
		} else if (rawEvent.Flags.HasFlag (FSEventStreamEventFlags.ItemCreated)) {
			// we can have a rename! so we need to check for that
			flag = TextFileChangedType.ItemCreated;
			if (rawEvent.Flags.HasFlag (FSEventStreamEventFlags.ItemRenamed)) {
				flag = TextFileChangedType.ItemRenamed;
			}
		} else if (rawEvent.Flags.HasFlag (FSEventStreamEventFlags.ItemRenamed)) {
			flag = TextFileChangedType.ItemRenamed;
		} else if (rawEvent.Flags.HasFlag (FSEventStreamEventFlags.ItemModified)) {
			// we can have a new file that was created and modified or just a modified file
			flag = rawEvent.Flags.HasFlag (FSEventStreamEventFlags.ItemCreated)
				? TextFileChangedType.ItemCreated
				: TextFileChangedType.ItemModified;
		}

		Log.Debug ("Returning flag {Flag} for {Flags}", flag, rawEvent.Flags);

		return (flag == TextFileChangedType.Unknown) ? null : new(rawEvent.Path!, flag);
	}

	public static TextFileChangedEvent FromFSEvent (FSEvent fromRawEvent, FSEvent toRawEvent) =>
		new (fromRawEvent.Path!, toRawEvent.Path!);
}
