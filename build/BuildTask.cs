using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Frosting;
using Spectre.Console;

namespace Build;

[TaskName("build")]
public sealed class BuildTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        AnsiConsole.MarkupLine(
            $"[blue]Building solution[/] [yellow]{context.SolutionFile.FullPath}[/] with configuration [yellow]{context.BuildConfiguration}[/].");

        using (context.UseDiagnosticVerbosity())
        {
            context.DotNetBuild(context.SolutionFile.FullPath, new DotNetBuildSettings
            {
                Configuration = context.BuildConfiguration,
            });
        }
    }
}
