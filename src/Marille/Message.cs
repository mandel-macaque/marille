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

	public Message (MessageType type)
	{
		Id = Guid.NewGuid();
		Type = type;
		Payload = default;
	}
	public Message (MessageType type, T payload) : this(type)
	{
		Id = Guid.NewGuid();
		Payload = payload;
	}
	public Message (T payload, Exception exception) : this(MessageType.Error, payload)
	{
		Exception = exception;
	}
}
