using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Tool;
using Cake.Frosting;
using Spectre.Console;

namespace Build;

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
