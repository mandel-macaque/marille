namespace Marille.Tests; 

public class MessageTests {
	
	[Fact]
	public void CreateMessageTest ()
	{
		var message = new Message<int> (MessageType.Data, 42, 0);
		Assert.Equal (MessageType.Data, message.Type);
		Assert.Equal (42, message.Payload);
		Assert.Equal (0, message.Retries);
		Assert.False (message.IsError);
		Assert.Null (message.Exception);
	}
	
	[Fact]
	public void CreateErrorMessageTest ()
	{
		var exception = new InvalidOperationException ("Test");
		var message = new Message<int> (42, exception);
		Assert.Equal (MessageType.Error, message.Type);
		Assert.Equal (42, message.Payload);
		Assert.Equal (0, message.Retries);
		Assert.True (message.IsError);
		Assert.Equal (exception, message.Exception);
	}
	
	[Fact]
	public void CreateMessageWithRetriesTest ()
	{
		var message = new Message<int> (MessageType.Data, 42, 5);
		Assert.Equal (MessageType.Data, message.Type);
		Assert.Equal (42, message.Payload);
		Assert.Equal (5, message.Retries);
		Assert.False (message.IsError);
		Assert.Null (message.Exception);
	}
}
