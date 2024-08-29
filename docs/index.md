# marille
Marille is thin layer on top of System.Threading.Channels that provides a in-process publisher/subscriber hub.

The aim of the library is to simplify the management of several `Channels<T>` within an application. The general idea
is that users can create different "topics" that will later be divided in subtopics based on the type of
messages that they distribute. The normal pattern for usage is as follows:

1. Create a topic, which is a pair of a topic name and a type of messages.
2. Add 1 or more workers which will be used to consume events.
3. Use the Hub to publish events to a topic.
4. Workers will pick the messages and consume them based on the topic strategy that was chosen.

You can think about Marille as an in-process pub/sub similar to a distributed queue with the difference
that the constraints are much simpler. Working within the same process makes dealing with failure much easier.

The library does not impose any thread usage and it fully relies on the default [TaskScheduler](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-threading-tasks-taskscheduler) used in 
the application.

## Examples

Here are some examples of the API usage:

### Single Publisher/Worker

In this pattern we will create a single topic with a unique consumer. The Hub will only send messages to the registered
consumer. 

1. Declare your consumer implementation:

```csharp
// define a message type to consumer
public record struct MyMessage (string Id, string Operation, int Value);

// create a worker type with the minimum implementation 
class MyWorker : IWorker<MyMessage> {
    
    // Workers will be by default considered to be IO bound unless specified otherwise
    public bool UseBackgroundThread => false;

    public Task ConsumeAsync (MyMessage message, CancellationToken cancellationToken = default)
       => Task.FromResult (Completion.TrySetResult(true));
}

// creeate an error worker to handle exceptions from workers
class ErrorWorker : IErrorWorker<MyMessage> {
    public bool UseBackgroundThread => false;
    
    public Task ConsumeAsync (MyMessage message, Exception exception, CancellationToken cancellationToken = default)
        => Task.FromResult (Completion.TrySetResult(true));
}

```

2. Setup the topic via a new Hub:

```csharp

private Hub _hub;

public Task CreateChannels () {
    _hub = new Hub ();
    // configuration to be used to create the topic
    var configuration = new() { Mode = ChannelDeliveryMode.AtMostOnce };
    var topic = "topic";
    var worker = new MyWorker ();
    var errorWorker = new ErrorWorker ();
    // workers can be either added during the channel creation or after the fact
    // with the TryRegister method.
    return _hub.CreateAsync (topic, configuration, errorWorer, worker);
}
```

Lambdas can also be registered as workers:

```csharp
Func<MyMessage, CancellationToken, Task> worker = (_, _) => Task.FromResult (true);
return _hub.CreateAsync (topic, configuration, worker);
```

3. Publish messages via the hub.

```csharp
public Task ProduceMessage (string id, string operation, int value) =>
    _hub.Publish (topic, new MyMessage (di, operation, value));
```

4. Cancel and wait for events to be completed

Becasue the entire library puspose of the library is to be able to process 
events in a multithreaded manner, the main thread has to wait until the events
are processed. That can be done by waiting on a Channel to be closed:

```csharp
// close a specific topic
await _hub.CloseAsync<MyMessage> (topic);

// close all topcis
await _hub.CloseAsync<MyMessage> (topic1);
```

5. Error handling

The library provides a way to handle exceptions that are thrown by the workers. The error worker is called
whenever an exception is thrown by a worker that is consuming a topic. It is up to the implementation to decide 
if the message should be retried or not. 

If we do not want to retry a message a worker can throw and exception, such exception will be added to a queue that 
will be consumed by the error worker that was used when the topic was created. Each topic has its own error queue, that
means that the error worker will only consume errors from the topic that it was created for.


