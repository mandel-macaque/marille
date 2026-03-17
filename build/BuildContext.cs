using Cake.Common;
using Cake.Core;
using Cake.Core.IO;
using Cake.Frosting;

namespace Build;

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
