using CoreServices;
using Marille;
using Marille.FileSystem.Events;
using Marille.FileSystem.Workers;
using Serilog;

namespace FSEvents.Workers;

public class FSEventFilterer(Hub hub) : EventFilterer<FSEvent> {
	readonly Hub _hub = hub;
	readonly List<FSEvent> _previous = new ();

	public override bool UseBackgroundThread => true;
	
	async Task FilterEvent (TextFileChangedEvent message)
	{
		Log.Debug ("Filtering event {Event}", message);
		switch (message.Flags) {
			case TextFileChangedType.ItemRenamed:
				if (IsValidMimeType (message.DestinationPath!)) {
					Log.Debug ("Processing rename event {Event} for file {Path} to {DestinationPath}", 
						message, message.Path, message.DestinationPath);
					await _hub.PublishAsync (nameof (FSMonitor), message);
				}
				break;
			case TextFileChangedType.ItemRemoved:
				// always push since we cannot know the mime type of the file
				await _hub.PublishAsync (nameof (FSMonitor), message);
				break;
			default:
				if (IsValidMimeType (message.Path!)) {
					Log.Debug ("Processing event {Event} for file {Path} with flags {Flags}", 
						message, message.Path, message.Flags);
					await _hub.PublishAsync (nameof (FSMonitor), message);
				}
				break;
		}
	}
	
	Task PumpEvents ()
	{
		return Task.CompletedTask;
	}

	public override async Task ConsumeAsync (FSEvent currentMessage, CancellationToken token = default)
	{
		Log.Information("Filtering event {Event}", currentMessage);

		if (currentMessage.Path is null) {
			Log.Debug ("Ignoring event with null path");
			return;
		}

		if (currentMessage.Path.Contains ("/.git/")) {
			Log.Debug ("Ignoring event for .git directory {Path}", currentMessage.Path);
			return;
		}

		// ignore directories
		if (currentMessage.Flags.HasFlag (FSEventStreamEventFlags.ItemIsDir)) {
			Log.Debug ("Ignoring event for directory {Path}", currentMessage.Path);
			return;
		}
		
		// the rename/move logic make things a little bit more complicated that we would like. Moves in the FSEvents
		// API are represented as 2 events. The move_from and the move_to event. Both events DO NOT always happen.
		//
		// * Only the move_from event happens when a file is moved out of the watched directory.
		// * Only the move_to event happens when a file is moved into the watched directory.
		// * Both events happen when a file is moved within the watched directory.
		// 
		// Possible scenarios:
		// * move_from -> move_to: 
		//   - mv watched/file.txt watched/file2.txt: consecutive ids, we can match the events.
		// * move_from:
		//  - mv watched/file.txt /tmp/file.txt: We will get a move_from event but never a move_to event. Any other event
		//    can happen that does not have a consecutive id. We will need to fake an event based on the stat of the file.
		// * move_to:
		//  - mv /tmp/file.txt watched/file.txt: We will get a move_to event but never a move_from event. Any other event
		//    can happen that does not have a consecutive id. We will need to fake an event based on the stat of the file.
		//
		// We will use a stack to keep track of the previous events and use that to decide what to do. The logic is as 
		// follows:
		// * if An event is not a rename and there is not a previous event, process the event.
		// * if there is a previous event we need to do the following:
		//  - if the current event is a rename:
		//    * ids are consecutive: we have a completed rename, process the event.
		//    * ids are not consecutive but paths are the same: we need to generate a fake
		//      edit event based on the stat of the file.
		//    * we have a rename followed by a rename: we have a completed rename, process the event and generate a fake
		//      event based on the stat of the file. Then push the current event to the stack.
		Log.Debug ("Previous events count is {Count}", _previous.Count);
		if (_previous.Count > 0) { 
			Log.Debug ("Events are {PreviousEvent} {CurrentEvent}", _previous[^1], currentMessage);
			// decide what to do based on the previous event
			if (currentMessage.Flags.HasFlag (FSEventStreamEventFlags.ItemRenamed) && currentMessage.Id - 1 != _previous[^1].Id) {
				Log.Debug ("Got partial with events {PreviousEvent} {CurrentEvent}", _previous[^1], currentMessage);
				_previous.Add (currentMessage);
				return;
			}

			// at this point we do know that either do not have a rename event OR we have a rename event with consecutive ids
			_previous.Add (currentMessage);

			Log.Debug ("Processing previous events");
			var textFileEvents = new Stack<TextFileChangedEvent> ();
			while (_previous.Count > 0) {
				var currentEvent = _previous[^1];
				_previous.RemoveAt (_previous.Count - 1);
				Log.Debug ("Processing event {Event}", currentEvent);
				// look at the current event, if it is not a rename, just create a new text file changed event and
				// add it to the stack, if it is a change event look at the previous event to see what needs to be 
				// done.
				if (currentEvent.Flags.HasFlag (FSEventStreamEventFlags.ItemRenamed)) {
					Log.Debug ("Found rename event {Event}", currentEvent);
					// we have a rename events, this means several things can happen:
					// 1. we have a completed rename. That happens when the current event and the previous event have
					//    consecutive ids. We can create a rename event and process it.
					// 2. we have a rename followed by a rename. That means that the previous rename is completed and we
					//    can create an event based on the stat of the file. 
					if (_previous.Count >= 1) { // we did remove the current event from the list, so we have to look for 1, not 2
						Log.Debug ("Found enough events to process a rename {Count}", _previous.Count);
						var previousEvent = _previous[^1]; 
						_previous.RemoveAt (_previous.Count - 1);
						Log.Debug ("Previous event is {Event}", previousEvent);
						if (currentEvent.Id - 1 == previousEvent.Id) { // remember we removed the current one from the list
							// we have a completed rename
							textFileEvents.Push (TextFileChangedEventFactory.FromFSEvent (previousEvent, currentEvent));
						} else { 
							// no matching events, generate two fake events based on the stat, add the current one and
							// then the previous since we are in a stack
							var previousStat = new FileInfo (previousEvent.Path!);
							textFileEvents.Push (previousStat.Exists
								? new (previousEvent.Path!, TextFileChangedType.ItemCreated)
								: new (previousEvent.Path!, TextFileChangedType.ItemRemoved));
							var currentStat = new FileInfo (currentEvent.Path!);
							textFileEvents.Push (currentStat.Exists
								? new (currentEvent.Path!, TextFileChangedType.ItemCreated)
								: new (currentEvent.Path!, TextFileChangedType.ItemRemoved));
						}
					} else {
						// we do not have more events and we have a rename, that means that we have a completed yet
						// mismatched rename. We create a new fake event based on the stat of the file.
						var stat = new FileInfo (currentEvent.Path!);
						textFileEvents.Push (stat.Exists
							? new(currentEvent.Path!, TextFileChangedType.ItemCreated)
							: new(currentEvent.Path!, TextFileChangedType.ItemRemoved));
					}
				} else {
					Log.Debug ("Adding event to the stack {Event}", currentEvent);
					// add to the stack a new event
					var textFileChangedEvent = TextFileChangedEventFactory.FromFSEvent (currentEvent);
					if (textFileChangedEvent is not null)
						textFileEvents.Push (textFileChangedEvent.Value);
				}
			}
			Log.Debug ("Generated {Count} events to be processed", textFileEvents.Count);
			// pump the events to the hub
			while (textFileEvents.TryPop (out var textFileChangedEvent)) {
				await FilterEvent (textFileChangedEvent);
			}
		} else {
			if (currentMessage.Flags.HasFlag (FSEventStreamEventFlags.ItemRenamed)) {
				Log.Debug ("Adding event {Event} to the end of the list", currentMessage);
				_previous.Add (currentMessage);
			} else {
				Log.Debug ("Pushing event {Event}", currentMessage);
				var textFileChangedEvent = TextFileChangedEventFactory.FromFSEvent (currentMessage);
				if (textFileChangedEvent is not null)
					await FilterEvent (textFileChangedEvent.Value);
				else
					Log.Debug ("Ignoring event {Event}", currentMessage);
			}
		}
	}

}
