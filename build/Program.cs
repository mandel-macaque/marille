using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.Test;
using Cake.Common.Tools.DotNet.Tool;
using Cake.Common;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;
using Spectre.Console;

namespace Build;

public static class Program
{
    public static int Main(string[] args) =>
        new CakeHost()
            .UseContext<BuildContext>()
            .Run(args);
}

public class BuildContext : FrostingContext
{
    public BuildContext(ICakeContext context)
        : base(context)
    {
        BuildConfiguration = context.Argument("configuration", "Release");
        SolutionFile = context.Environment.WorkingDirectory.CombineWithFilePath("src/marille.sln");
        DocsDirectory = context.Environment.WorkingDirectory.Combine("docs");
        DocfxConfig = DocsDirectory.CombineWithFilePath("docfx.json");
        ToolManifest = context.Environment.WorkingDirectory.CombineWithFilePath("dotnet-tools.json");
    }

    public string BuildConfiguration { get; }

    public FilePath SolutionFile { get; }

    public DirectoryPath DocsDirectory { get; }

    public FilePath DocfxConfig { get; }

    public FilePath ToolManifest { get; }
}

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
            });
        }
    }
}

[TaskName("generate-docs")]
[IsDependentOn(typeof(BuildTask))]
public sealed class GenerateDocsTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        AnsiConsole.MarkupLine(
            $"[blue]Restoring dotnet tools[/] using manifest [yellow]{context.ToolManifest.FullPath}[/].");

        using (context.UseDiagnosticVerbosity())
        {
            var restoreCommand = $"tool restore --tool-manifest \"{context.ToolManifest.FullPath}\"";
            context.DotNetTool(restoreCommand);
        }

        AnsiConsole.MarkupLine(
            $"[blue]Generating documentation[/] with docfx configuration [yellow]{context.DocfxConfig.FullPath}[/].");

        using (context.UseDiagnosticVerbosity())
        {
            var docfxCommand = $"tool run docfx build \"{context.DocfxConfig.FullPath}\"";
            context.DotNetTool(docfxCommand, new DotNetToolSettings
            {
                WorkingDirectory = context.DocsDirectory,
            });
        }
    }
}

[TaskName("default")]
[IsDependentOn(typeof(BuildTask))]
public sealed class DefaultTask : FrostingTask
{
}
