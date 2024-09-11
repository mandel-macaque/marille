namespace Marille.Tests; 

public class TopicConfigurationTests {
	
	[Fact]
	public void CreateTopicConfigurationTest ()
	{
		var configuration = new TopicConfiguration();
		Assert.Equal (ChannelDeliveryMode.AtLeastOnceAsync, configuration.Mode);
		Assert.Null (configuration.MaxRetries);
		Assert.Null (configuration.MaxCapacity);
		Assert.Null (configuration.Timeout);
	}
	
	[Fact]
	public void CreateTopicConfigurationWithValuesTest ()
	{
		var configuration = new TopicConfiguration() {
			Mode = ChannelDeliveryMode.AtMostOnceAsync,
			MaxRetries = 5,
			MaxCapacity = 10,
			Timeout = TimeSpan.FromSeconds(5)
		};
		Assert.Equal (ChannelDeliveryMode.AtMostOnceAsync, configuration.Mode);
		Assert.Equal (5u, configuration.MaxRetries);
		Assert.Equal (10, configuration.MaxCapacity);
		Assert.Equal (TimeSpan.FromSeconds(5), configuration.Timeout);
	}
}
