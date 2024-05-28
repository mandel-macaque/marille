namespace Marille;

/// <summary>
/// Represents the delivery semantics to be used with a topic.
/// </summary>
public enum ChannelDeliveryMode {
	/// <summary>
	/// Messages in topics will be delivered to all workers.
	/// </summary>
	AtLeastOnce,
	/// <summary>
	/// Messages in topics will be delivered just to a single worker and the processing of a
	/// message will NOT await for the task of the previous one.
	/// </summary>
	AtMostOnceAsync,
	/// <summary>
	/// Messages in topics will be delivered just to a single worker and the processing of a
	/// message will AWAIT for the task of the previous one.
	/// </summary>
	AtMostOnceSync,
}
