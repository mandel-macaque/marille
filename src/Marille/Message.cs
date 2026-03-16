using System.Diagnostics.CodeAnalysis;

namespace Marille;

internal enum MessageType {
	Ack,
	Data,
	Error,
}

internal struct Message<T> (MessageType type, T payload = default, uint? retries = null, Exception? exception = null)
	where T : struct {
	public Guid Id { get; } = Guid.NewGuid ();
	public MessageType Type { get; } = type;

	[MemberNotNullWhen(true, nameof(Exception))]
	public bool IsError => Type == MessageType.Error;
	public T Payload { get; } = payload;
	public Exception? Exception { get; } = exception;
	public int Retries = (int)(retries ?? 0);
	
	public Message (T payload, Exception exception) : this (MessageType.Error, payload, null, exception) { }
}
