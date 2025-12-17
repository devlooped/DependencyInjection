using System.Diagnostics;
using Microsoft.CodeAnalysis.Diagnostics;

static class CodeAnalysisExtensions
{
    /// <summary>
    /// Gets whether the current build is a design-time build.
    /// </summary>
    public static bool IsDesignTimeBuild(this AnalyzerConfigOptionsProvider options) =>
#if DEBUG
        // Assume if we have a debugger attached to a debug build, we want to debug the generator
        !Debugger.IsAttached &&
#endif
        options.GlobalOptions.TryGetValue("build_property.DesignTimeBuild", out var value) &&
        bool.TryParse(value, out var isDesignTime) && isDesignTime;

    /// <summary>
    /// Gets whether the current build is a design-time build.
    /// </summary>
    public static bool IsDesignTimeBuild(this AnalyzerConfigOptions options) =>
#if DEBUG
        // Assume if we have a debugger attached to a debug build, we want to debug the generator
        !Debugger.IsAttached &&
#endif
        options.TryGetValue("build_property.DesignTimeBuild", out var value) &&
        bool.TryParse(value, out var isDesignTime) && isDesignTime;
}