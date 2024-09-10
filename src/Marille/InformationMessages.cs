using Microsoft.Extensions.Logging;

namespace Marille;

internal static partial class InformationMessages {


	[LoggerMessage (
		Message = "Created topic {TopicName} of {TopicType}",
		Level = LogLevel.Information)]
	internal static partial void LogCreateTopic (this ILogger logger, string topicName, Type topicType);
	
	[LoggerMessage (
		Message = "Registering to {TopicName} of {TopicType} {WorkerCount} workers",
		Level = LogLevel.Information)]
	internal static partial void LogRegisterWorkers (this ILogger logger, string topicName, Type topicType, int workerCount);

	[LoggerMessage (
		Message = "Publishing async to {TopicName} {Message}",
		Level = LogLevel.Information)]
	internal static partial void LogPublishAsyncEvent (this ILogger logger, string topicName, object message);
	
	[LoggerMessage (
		Message = "TryPublishing to {TopicName} {Message}",
		Level = LogLevel.Information)]
	internal static partial void LogTryPublishEvent (this ILogger logger, string topicName, object message);

	[LoggerMessage (
		Message = "All channels closed",
		Level = LogLevel.Information)]
	internal static partial void LogCloseAllAsync (this ILogger logger);
	
	[LoggerMessage (
		Message = "Close async of {TopicName} of {TopicType}",
		Level = LogLevel.Information)]
	internal static partial void LogCloseAsync (this ILogger logger, string topicName, Type topicType);
}
