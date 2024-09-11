using System.Diagnostics.CodeAnalysis;

namespace Marille;

internal enum MessageType {
	Ack,
	Data,
	Error,
}
internal struct Message<T> where T : struct {
	public Guid Id { get; }
	public MessageType Type { get; }

	[MemberNotNullWhen(true, nameof(Exception))]
	public bool IsError => Type == MessageType.Error;
	public T Payload { get; }
	public Exception? Exception { get; }
	public int Retries = 0;
	
	public Message (MessageType type)
	{
		Id = Guid.NewGuid();
		Type = type;
		Payload = default;
	}
	public Message (MessageType type, T payload, uint? retries) : this(type)
	{
		Id = Guid.NewGuid();
		Payload = payload;
		Retries = (int) (retries ?? 0);
	}
	public Message (T payload, Exception exception) : this(MessageType.Error, payload, null)
	{
		Exception = exception;
	}
}
