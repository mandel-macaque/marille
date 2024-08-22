namespace Marille.Tests;

public struct WorkQueuesEvent {

	public string Id { get; private set;}
	public bool IsError { get; private set; }
	public WorkQueuesEvent (string id, bool isError = false)
	{
		Id = id;
		IsError = isError;
		
	}
}

public record WorkDoneEvent (string WorkerId);

public class FastWorker : IWorker<WorkQueuesEvent> {
	public string Id { get; set; } = string.Empty;
	public TaskCompletionSource<bool> Completion { get; }

	public FastWorker (string id, TaskCompletionSource<bool> tcs)
	{
		Id = id;
		Completion = tcs;
	}

	public Task ConsumeAsync (WorkQueuesEvent message, CancellationToken cancellationToken = default)
		=> message.IsError ? 
			Task.FromException (new InvalidOperationException($"Message with Id {message.Id} is an error")) :
			Task.FromResult (Completion.TrySetResult(true));

	public void Dispose () { }

	public ValueTask DisposeAsync () => ValueTask.CompletedTask;
}
