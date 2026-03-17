using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Test;
using Cake.Core.IO;
using Cake.Frosting;
using Spectre.Console;

namespace Build;

[TaskName("run-tests")]
[IsDependentOn(typeof(BuildTask))]
public sealed class RunTestsTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        AnsiConsole.MarkupLine("[blue]Running tests[/] for solution.");

        using (context.UseDiagnosticVerbosity())
        {
            context.DotNetTest(context.SolutionFile.FullPath, new DotNetTestSettings
            {
                Configuration = context.BuildConfiguration,
                NoBuild = true,
                ResultsDirectory = context.Environment.WorkingDirectory.Combine("TestResults"),
                Loggers = new[] { "trx;LogFileName=tests.trx" },
            });
        }
    }
}
