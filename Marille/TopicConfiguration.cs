namespace Marille;

/// <summary>
/// Configuration class used to manage several settings for the channel creation.
/// </summary>
public struct TopicConfiguration () {
	/// <summary>
	/// The mode that will be used to dispatch messages among the different workers.
	/// </summary>
	public	ChannelDeliveryMode Mode { get; set; } = ChannelDeliveryMode.AtLeastOnce;

	/// <summary>
	/// The capacity that the channel will have. When a channel is full, producers will be
	/// blocked until there is enough space.
	///
	/// When no capacity (null) is provided, the channel won't have a capacity and producers will
	/// be able to write in the channel as much as they want as long as it is opened.
	/// </summary>
	public int? Capacity { get; set; } = null;
	
	/// <summary>
	/// Default timeout for the workers that consume messages in the channel. 
	/// </summary>
	public TimeSpan? Timeout { get; set; } = null;
}
