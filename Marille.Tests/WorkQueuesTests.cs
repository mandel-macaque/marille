using System.Threading.Channels;

namespace Marille.Tests;

// Set of tests that focus on the pattern in which a 
// several consumers register to a queue and the compete
// to consume an event.
public class WorkQueuesTests
{
    public record WorkQueuesEvent(string Id);

    public record WorkDoneEvent(string WorkerId);

    public class FastWorker : IWorker<WorkQueuesEvent>
    {
        public string Id { get; set; }
    }

    public class SlowWorker : IWorker<WorkQueuesEvent>
    {
        public string Id { get; set; }
    }

    public class WorkQueuesTestHub : Hub
    {
        
    }

    private Hub _hub;
    private Channel<WorkDoneEvent> _completedChannel;
    private CancellationTokenSource _cancellationTokenSource;

    public WorkQueuesTests()
    {
        // use a simpler channel that we will use to receive the events when
        // a worker has completed its work
        _hub = new WorkQueuesTestHub ();
        _completedChannel = Channel.CreateUnbounded<WorkDoneEvent>();
        _cancellationTokenSource = new();
    }
    
    // The simplest API usage, we register to an event for a topic with our worker
    // and the event should be consumed and added to the completed channel for use to verify
    [Fact]
    public async void SingleWorker ()
    {
        var worker = new FastWorker { Id = "myWorkerID" };
        _hub.TryRegister("topic", worker);
        await _hub.Publish("topic", new WorkDoneEvent("myID"));
        // use read async with a timeout from our cancelation token
        _cancellationTokenSource.CancelAfter(100);
        var workerResult = 
            await _completedChannel.Reader.ReadAsync(_cancellationTokenSource.Token);
        Assert.Equal(worker.Id, workerResult.WorkerId);
    }

    [Fact]
    public async void SingleAction()
    {
        var workerID = "myWorkerID";
        Action<WorkQueuesEvent> action = (channelEvent) =>
        {
            _completedChannel.Writer.TryWrite(new WorkDoneEvent(workerID));
        };
        
        Assert.True(_hub.TryRegister("topic", action));
        await _hub.Publish("topic", new WorkDoneEvent("myID"));
        
        // use read async with a timeout from our cancellation token
        _cancellationTokenSource.CancelAfter(100);
        var workerResult = 
            await _completedChannel.Reader.ReadAsync(_cancellationTokenSource.Token);
        Assert.Equal(workerID, workerResult.WorkerId);
    }
    
    [Fact]
    public async void SeveralWorkers ()
    {
        var workerID = "myWorkerID";
        var topic1 = "topic1";
        var topic2 = "topic2";
        // ensure that each of the workers will receive data for the topic it is interested
        var worker1 = new FastWorker();
        Assert.True(_hub.TryRegister(topic1, worker1));
        
        Action<WorkQueuesEvent> worker2 = _ => { };
        Assert.True(_hub.TryRegister(topic2, worker2));
        
        // publish two both topics and ensure that each of the workers gets teh right data
        Task<WorkDoneEvent> [] publishTasks = [
            _hub.Publish(topic1, new WorkDoneEvent("")),
            _hub.Publish(topic2, new WorkDoneEvent(""))];
        await Task.WhenAll(publishTasks);
        // we should only get the event in topic one, the second topic should be ignored
        var workerResult = 
            await _completedChannel.Reader.ReadAsync(_cancellationTokenSource.Token);
        Assert.Equal(workerID, workerResult.WorkerId);
        // we should not be able to read anymore
        Assert.False(_completedChannel.Reader.TryRead(out var @event));
    }

    [Fact]
    public void SlowFastWorker()
    {
    }
}