using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.Tooling;

namespace Build;

/// <summary>
/// Helper methods for Cake context.
/// </summary>
public static class ICakeContextExtensions
{
    /// <summary>
    /// Temporary sets logging verbosity.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="newVerbosity">The new verbosity.</param>
    /// <returns>Disposable object that restores old verbosity on Dispose.</returns>
    /// <example>
    /// <code>
    /// // Temporary sets logging verbosity to Diagnostic.
    /// using(context.UseVerbosity(Verbosity.Diagnostic))
    /// {
    ///     context.DotNetBuild(project, settings);
    /// }
    /// </code>
    /// </example>
    public static VerbosityChanger UseVerbosity(
        this ICakeContext context,
        Verbosity newVerbosity
    ) => new(context.Log, newVerbosity);

    /// <summary>
    /// Temporary sets logging verbosity to Diagnostic.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <returns>Disposable object that restores old verbosity on Dispose.</returns>
    /// <example>
    /// <code>
    /// // Temporary sets logging verbosity to Diagnostic.
    /// using(context.UseDiagnosticVerbosity())
    /// {
    ///     context.DotNetBuild(project, settings);
    /// }
    /// </code>
    /// </example>
    public static VerbosityChanger UseDiagnosticVerbosity(this ICakeContext context) =>
        context.UseVerbosity(Verbosity.Diagnostic);

    /// <summary>
    /// Determines whether a specified tool is available in the current environment.
    /// </summary>
    /// <param name="context">The Cake context.</param>
    /// <param name="toolName">The name of the tool to check for availability.</param>
    /// <returns>True if the tool is available; otherwise, false.</returns>
    public static bool IsToolAvailable(this ICakeContext context, string toolName)
    {
        var settings = new ToolSettings { ToolPath = context.Tools.Resolve(toolName) };

        return settings.ToolPath is not null;
    }
}

/// <summary>
/// Changes Cake logging verbosity for the lifetime of the instance.
/// </summary>
public sealed class VerbosityChanger : IDisposable
{
    private readonly ICakeLog _log;
    private readonly Verbosity _originalVerbosity;

    public VerbosityChanger(ICakeLog log, Verbosity newVerbosity)
    {
        _log = log;
        _originalVerbosity = log.Verbosity;
        _log.Verbosity = newVerbosity;
    }

    public void Dispose()
    {
        _log.Verbosity = _originalVerbosity;
    }
}
