namespace Marille;

internal enum MessageType {
	Ack,
	Data
}
internal struct Message<T> where T : struct {
	public Guid Id { get; }
	public MessageType Type { get; }
	public T Payload { get; }

	public Message (MessageType type)
	{
		Id = Guid.NewGuid();
		Type = type;
		Payload = default;
	}
	public Message (MessageType type, T payload)
	{
		Id = Guid.NewGuid();
		Type = type;
		Payload = payload;
	}
}
