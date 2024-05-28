namespace Marille.Tests;

public struct WorkQueuesEvent (string Id);

public record WorkDoneEvent (string WorkerId);

public class FastWorker : IWorker<WorkQueuesEvent> {
	public string Id { get; set; } = string.Empty;
	public TaskCompletionSource<bool> Completion { get; private set; }

	public FastWorker (string id, TaskCompletionSource<bool> tcs)
	{
		Id = id;
		Completion = tcs;
	}

	public Task ConsumeAsync (WorkQueuesEvent message, CancellationToken cancellationToken = default)
		=> Task.FromResult (Completion.TrySetResult(true));
}
