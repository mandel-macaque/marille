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
		Assert.Null (configuration.MaxParallelism);
	}
	
	[Fact]
	public void CreateTopicConfigurationWithValuesTest ()
	{
		var configuration = new TopicConfiguration() {
			Mode = ChannelDeliveryMode.AtMostOnceAsync,
			MaxRetries = 5,
			MaxCapacity = 10,
			MaxParallelism = 15,
			Timeout = TimeSpan.FromSeconds(5)
		};
		Assert.Equal (ChannelDeliveryMode.AtMostOnceAsync, configuration.Mode);
		Assert.NotNull(configuration.MaxRetries);
		Assert.Equal (5u, configuration.MaxRetries.Value);
		Assert.NotNull (configuration.MaxCapacity);
		Assert.Equal (10, configuration.MaxCapacity.Value);
		Assert.NotNull (configuration.MaxParallelism);
		Assert.Equal (15u, configuration.MaxParallelism.Value);
		Assert.Equal (TimeSpan.FromSeconds(5), configuration.Timeout);
	}
}
