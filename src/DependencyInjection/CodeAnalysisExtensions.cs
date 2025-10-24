using System;
using Microsoft.CodeAnalysis.Diagnostics;

static class CodeAnalysisExtensions
{
    /// <summary>
    /// Whether the current process is running in an IDE, either 
    /// <see cref="IsVisualStudio"/> or <see cref="IsRider"/>.
    /// </summary>
    public static bool IsEditor => IsVisualStudio || IsRider;

    /// <summary>
    /// Whether the current process is running as part of an active Visual Studio instance.
    /// </summary>
    public static bool IsVisualStudio =>
        Environment.GetEnvironmentVariable("ServiceHubLogSessionKey") != null ||
        Environment.GetEnvironmentVariable("VSAPPIDNAME") != null;

    /// <summary>
    /// Whether the current process is running as part of an active Rider instance.
    /// </summary>
    public static bool IsRider =>
        Environment.GetEnvironmentVariable("RESHARPER_FUS_SESSION") != null ||
        Environment.GetEnvironmentVariable("IDEA_INITIAL_DIRECTORY") != null;

    /// <summary>
    /// Gets whether the current build is a design-time build.
    /// </summary>
    public static bool IsDesignTimeBuild(this AnalyzerConfigOptionsProvider options) =>
        options.GlobalOptions.TryGetValue("build_property.DesignTimeBuild", out var value) &&
        bool.TryParse(value, out var isDesignTime) && isDesignTime;

    /// <summary>
    /// Gets whether the current build is a design-time build.
    /// </summary>
    public static bool IsDesignTimeBuild(this AnalyzerConfigOptions options) =>
        options.TryGetValue("build_property.DesignTimeBuild", out var value) &&
        bool.TryParse(value, out var isDesignTime) && isDesignTime;
}