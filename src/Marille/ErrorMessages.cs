using Microsoft.Extensions.Logging;

namespace Marille;

internal static partial class ErrorMessages {
	
	[LoggerMessage(
		Message = "Unable to create topic {TopicName} of {TopicType} with {Mode} and {WorkerCount} workers", 
		Level = LogLevel.Error,
		SkipEnabledCheck = true)]
	internal static partial void LogWrongWorkerCount (this ILogger logger, string topicName, Type topicType, 
		ChannelDeliveryMode mode, int workerCount);	
	
	[LoggerMessage(
		Message = "Topic {TopicName} of {TopicType} already exists", 
		Level = LogLevel.Error,
		SkipEnabledCheck = true)]
	internal static partial void LogTopicAlreadyExists (this ILogger logger, string topicName, Type topicType);
	
	[LoggerMessage(
		Message = "Cannot register workers to none existing {TopicName} of {TopicType}", 
		Level = LogLevel.Error,
		SkipEnabledCheck = true)]
	internal static partial void LogRegisterErrorTopicDoesNotExist (this ILogger logger, string topicName, 
		Type topicType);
	
	[LoggerMessage(
		Message = "Cannot register to {TopicName} of {TopicType} of {Mode} with {WorkerCount} workers", 
		Level = LogLevel.Error,
		SkipEnabledCheck = true)]
	internal static partial void LogRegisterErrorTooManyWorkers (this ILogger logger, string topicName, Type topicType, 
		ChannelDeliveryMode mode, int workerCount);
	
	[LoggerMessage(
		Message = "Cannot close none existing {TopicName} of {TopicType}", 
		Level = LogLevel.Error,
		SkipEnabledCheck = true)]
	internal static partial void LogCloseErrorTopicDoesNotExist (this ILogger logger, string topicName, Type topicType);
	
	[LoggerMessage(
		Message = "ErrorWorker {ErrorWorker} failed to process message {Message} on {TopicName} of {TopicType}", 
		Level = LogLevel.Error,
		SkipEnabledCheck = true)]
	internal static partial void LogErrorConsumerException (this ILogger logger, Type errorWorker, object message, 
		Exception exception, string topicName, Type topicType);
	
	[LoggerMessage(
		Message = "Worker {Worker} raised OnChannelClose for {TopicName} of {TopicType}",
		Level = LogLevel.Error,
		SkipEnabledCheck = true)]
	internal static partial void LogErrorOnChannelClose (this ILogger logger, Type worker, Exception exceptionOnClose, 
		string topicName, Type topicType);
}
