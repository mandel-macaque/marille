using Microsoft.Extensions.Logging;

namespace Marille;

internal static class TimeSpanExtensions {
	
	public static CancellationToken GetCancellationToken (this TimeSpan? timeout, ILogger? logger)
	{
		if (!timeout.HasValue) 
			return default;
		
		CancellationToken token = default;
		logger?.LogTraceTimeoutCreation (timeout.Value);
		var cts = new CancellationTokenSource ();
		cts.CancelAfter (timeout.Value);
		token = cts.Token;

		return token;
	}
}
