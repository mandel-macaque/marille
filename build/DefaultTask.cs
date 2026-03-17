using Cake.Frosting;

namespace Build;

[TaskName("default")]
[IsDependentOn(typeof(BuildTask))]
public sealed class DefaultTask : FrostingTask
{
}
