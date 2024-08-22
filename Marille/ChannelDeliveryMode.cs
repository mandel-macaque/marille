namespace Marille;

/// <summary>
/// Represents the delivery semantics to be used with a topic.
/// </summary>
public enum ChannelDeliveryMode {
	/// <summary>
	/// Messages in topics will be delivered to all workers.
	/// </summary>
	AtLeastOnceAsync = 0,
	/// <summary>
	/// Messages in topics will be delivered to all workers and the processing of a message will
	/// wait for the tasks of the previous one.
	/// </summary>
	AtLeastOnceSync = 1,
	/// <summary>
	/// Messages in topics will be delivered just to a single worker and the processing of a
	/// message will NOT await for the task of the previous one.
	/// </summary>
	AtMostOnceAsync = 2,
	/// <summary>
	/// Messages in topics will be delivered just to a single worker and the processing of a
	/// message will AWAIT for the task of the previous one.
	/// </summary>
	AtMostOnceSync = 3,
}
