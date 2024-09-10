using Microsoft.Extensions.Logging;

namespace Marille; 

internal static partial class CriticalMessages {
	
	[LoggerMessage(
		Message = "InvalidOperationException publishing to non existing {TopicName} of {TopicType}", 
		Level = LogLevel.Critical,
		SkipEnabledCheck = true)]
	internal static partial void LogPublishErrorTopicDoesNotExist (this ILogger logger, string topicName, 
		Type topicType);
}
