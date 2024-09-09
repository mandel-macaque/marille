namespace Marille.Tests; 

// base test class that will create a cancellation token for the unit tests
// to use
public class BaseTimeoutTest {
	private readonly int _timeout;
	protected BaseTimeoutTest (int milliseconds)
	{
		_timeout = milliseconds;
	}

	protected CancellationTokenSource GetCancellationToken ()
	{
		var cts = new CancellationTokenSource ();
		cts.CancelAfter (_timeout);
		return cts;
	}
}
