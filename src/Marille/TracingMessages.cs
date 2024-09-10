using Microsoft.Extensions.Logging;

namespace Marille; 

internal static partial class TracingMessages {
	
	[LoggerMessage(
		Message = "Consume message from {TopicName} of {TopicType} {Message}",
		Level = LogLevel.Trace)]
	internal static partial void LogTraceConsumeMessage (this ILogger logger, string topicName, Type topicType, 
		object message);	
	
	[LoggerMessage(
		Message = "ACK received for {TopicName} of {TopicType}",
		Level = LogLevel.Trace)]
	internal static partial void LogTraceAckReceived  (this ILogger logger, string topicName, Type topicType);
	
	[LoggerMessage(
		Message = "Consume Error message from {TopicName} of {TopicType} {Message}",
		Level = LogLevel.Trace)]
	internal static partial void LogTraceErrorReceived (this ILogger logger, string topicName, Type topicType, 
		object message, Exception exception);
	
	[LoggerMessage(
		Message = "Call OnChannelClose on {Worker} for {TopicName} of {TopicType}",
		Level = LogLevel.Trace)]
	internal static partial void LogTraceCallOnChannelClose (this ILogger logger, Type worker, string topicName, 
		Type topicType);
	
	[LoggerMessage(
		Message = "DeliverAtLeastOnceAsync {Message} to {TopicName} of {TopicType}",
		Level = LogLevel.Trace)]
	internal static partial void LogTraceDeliverAtLeastOnceAsync (this ILogger logger, object message, string topicName, 
		Type topicType);
	
	[LoggerMessage(
		Message = "DeliverAtLeastOnceSync {Message} to {TopicName} of {TopicType}",
		Level = LogLevel.Trace)]
	internal static partial void LogTraceDeliverAtLeastOnceSync (this ILogger logger, object message, string topicName, 
		Type topicType);
	
	[LoggerMessage(
		Message = "DeliverAtMostOnceAsync {Message} to {TopicName} of {TopicType}",
		Level = LogLevel.Trace)]
	internal static partial void LogTraceDeliverAtMostOnceAsync (this ILogger logger, object message, string topicName, 
		Type topicType);

	[LoggerMessage(
		Message = "Creating timeout of {TimeoutValue}",
		Level = LogLevel.Trace)]
	internal static partial void LogTraceTimeoutCreation (this ILogger logger, TimeSpan timeoutValue);
	
	[LoggerMessage(
		Message = "Pumping error message from {TopicName} of {TopicType} {Message}",
		Level = LogLevel.Trace)]
	internal static partial void LogTracePumpErrorMessage (this ILogger logger, string topicName, Type topicType, 
		object message, Exception exception); 
	
	[LoggerMessage(
		Message = "Retry on cancel message from {TopicName} of {TopicType} {Message}",
		Level = LogLevel.Trace)]
	internal static partial void LogTraceRetryOnCancel (this ILogger logger, string topicName, Type topicType, 
		object message);
}
