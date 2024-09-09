using Marille.Tests.Workers;

namespace Marille.Tests;

public class TopicTests : IDisposable {

	readonly Topic _topic;
	readonly TopicConfiguration _configuration;
	public TopicTests ()
	{
		_topic = new (nameof(TopicTests));
		_configuration = new() { Mode = ChannelDeliveryMode.AtMostOnceAsync };
	}
	
	public void Dispose ()
	{
		_topic.Dispose ();
	}

	[Fact]
	public void Constructor ()
	{
		Assert.Equal (nameof(TopicTests), _topic.Name);
	}

	[Fact]
	public void AddSameNameDiffEvent ()
	{
		// add several channels with different events and ensure that we only have the right amount
		var errorWorker = new ErrorWorker<int> ();
		var errorWorker2 = new ErrorWorker<WorkQueuesEvent> ();
		Assert.True (_topic.TryCreateChannel (_configuration, out var info, errorWorker));
		Assert.True (_topic.TryCreateChannel (_configuration, out var info2, errorWorker2));
		Assert.NotSame (info, info2);
	}
	
	[Fact]
	public void AddSameNameSameEvent ()
	{
		// ensure that we do not recreate the channel
		var errorWorker = new ErrorWorker<int> ();
		Assert.True (_topic.TryCreateChannel (_configuration, out var info, errorWorker));
		Assert.False (_topic.TryCreateChannel (_configuration, out var info2, errorWorker));
		Assert.Same (info, info2);
	}
	
	[Fact]
	public void RemoveOnlyChannel ()
	{
		// remove the channel and ensure that it will be empty
		var errorWorker = new ErrorWorker<int> ();
		Assert.True (_topic.TryCreateChannel (_configuration, out _, errorWorker));
		Assert.Equal (1, _topic.ChannelCount);
		_topic.RemoveChannel<int> ();
		Assert.Equal (0, _topic.ChannelCount);
	}
	
	[Fact]
	public void RemoveWithSeveralChannels ()
	{
		// ensure that only once channel will be removed
		var errorWorker = new ErrorWorker<int> ();
		var errorWorker2 = new ErrorWorker<WorkQueuesEvent> ();
		Assert.True (_topic.TryCreateChannel (_configuration, out _, errorWorker));
		Assert.True (_topic.TryCreateChannel (_configuration, out _, errorWorker2));
		Assert.Equal (2, _topic.ChannelCount);
		_topic.RemoveChannel<int> ();
		Assert.Equal (1, _topic.ChannelCount);
	}
	
}
