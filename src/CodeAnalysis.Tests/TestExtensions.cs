using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities.Resources;

namespace Tests.CodeAnalysis;

public static class TestExtensions
{
    public static TAnalyzerTest WithPreprocessorSymbols<TAnalyzerTest>(this TAnalyzerTest test, params string[] symbols)
        where TAnalyzerTest : AnalyzerTest<DefaultVerifier>
    {
        test.OptionsTransforms.Add(options =>
        {
            return options;
        });

        test.SolutionTransforms.Add((solution, projectId) =>
        {
            var project = solution.GetProject(projectId);
            var parseOptions = (CSharpParseOptions)project!.ParseOptions!;

            parseOptions = parseOptions.WithPreprocessorSymbols(
                symbols.Length > 0 ? symbols : ["DDI_ADDSERVICE", "DDI_ADDSERVICES"]);

            return solution.WithProjectParseOptions(projectId, parseOptions);
        });

        return test;
    }

    public static TAnalyzerTest WithTestState<TAnalyzerTest>(this TAnalyzerTest test)
        where TAnalyzerTest : AnalyzerTest<DefaultVerifier>
    {
        test.TestState.Sources.AddStaticFiles();
        test.TestState.ReferenceAssemblies = new ReferenceAssemblies(
            "net8.0",
            new PackageIdentity(
                "Microsoft.NETCore.App.Ref", "8.0.0"),
                Path.Combine("ref", "net8.0"))
            .AddPackages(ImmutableArray.Create(
                new PackageIdentity("Microsoft.Extensions.DependencyInjection", "8.0.0")));

        return test;
    }

    public static void AddStaticFiles(this SourceFileList sources)
    {
        sources.Add(ThisAssembly.Resources.AddServicesNoReflectionExtension.Text);
        sources.Add(ThisAssembly.Resources.ServiceAttribute.Text);
        sources.Add(ThisAssembly.Resources.ServiceAttribute_1.Text);
    }
}
