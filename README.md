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

The library does not impose any thread usage and it fully relies on the default [TaskScheduler](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-threading-tasks-taskscheduler) used in the application, that is, there are no Task.Run calls to be found in the 
source of the library. 

## Examples

Here are some examples of the API usage:

### Single Publisher/Worker

In this pattern we will create a single topic with a unique consumer. The Hub will only send messages to the registered
consumer. 

1. Declare your consumer implementation:

```csharp
// define a message type to consumer
public record MyMessage (string Id, string Operation, int Value);

// create a worker type with the minimum implementation 
class MyWorker : IWorker<MyMessage> {
    // Workers will be scheduled by the default TaskScheduler from dotnet
    public Task ConsumeAsync (WorkQueuesEvent message, CancellationToken cancellationToken = default)
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
    // workers can be either added during the channel creation or after the fact
    // with the TryRegister method.
    return _hub.CreateAsync<MyMessage> (topic, configuration, worker);
}
```

Lambdas can also be registered as workers:

```csharp
Func<MyMessage, CancellationToken, Task> worker = (_, _) => Task.FromResult (true);
return _hub.CreateAsync<MyMessage> (topic, configuration, worker);
```

3. Publish messages via the hub.

```csharp
public Task ProduceMessage (string id, string operation, int value) =>
    _hub.Publish (topic, new MyMessage (di, operation, value));
```
