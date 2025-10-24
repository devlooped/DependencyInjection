using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Devlooped.Extensions.DependencyInjection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Xunit.Abstractions;
using AnalyzerTest = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<Devlooped.Extensions.DependencyInjection.AddServicesAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<Devlooped.Extensions.DependencyInjection.AddServicesAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Tests.CodeAnalysis;

public class AddServicesAnalyzerTests(ITestOutputHelper Output)
{
    [Fact]
    public async Task NoWarningIfAddServicesPresent()
    {
        var test = new GeneratorsTest
        {
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestCode =
            """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            public record Command;
            
            public static class Program
            {
                public static void Main()
                {
                    var services = new ServiceCollection();
                    services.AddServices();
                }
            }
            """,
            TestState =
            {
                Sources =
                {
                    ThisAssembly.Resources.AddServicesNoReflectionExtension.Text,
                    ThisAssembly.Resources.ServiceAttribute.Text,
                    ThisAssembly.Resources.ServiceAttribute_1.Text
                },
                ReferenceAssemblies = new ReferenceAssemblies(
                    "net8.0",
                    new PackageIdentity(
                        "Microsoft.NETCore.App.Ref", "8.0.0"),
                        Path.Combine("ref", "net8.0"))
                    .AddPackages(ImmutableArray.Create(
                        new PackageIdentity("Microsoft.Extensions.DependencyInjection", "8.0.0")))
            },
        }.WithPreprocessorSymbols();

        //var expected = Verifier.Diagnostic(AddServicesAnalyzer.NoAddServicesCall).WithLocation(0);

        //test.ExpectedDiagnostics.Add(expected);
        //test.ExpectedDiagnostics.Add(new DiagnosticResult("CS1503", DiagnosticSeverity.Error).WithLocation(0));

        await test.RunAsync();
    }

    [Fact]
    public async Task NoWarningIfNoServiceCollectionCalls()
    {
        var test = new GeneratorsTest
        {
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestCode = """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            public record Command;
            
            public static class Program
            {
                public static void Main()
                {
                    Console.WriteLine("Hello World!");
                }
            }
            """,
            TestState =
            {
                Sources =
                {
                    ThisAssembly.Resources.AddServicesNoReflectionExtension.Text,
                    ThisAssembly.Resources.ServiceAttribute.Text,
                    ThisAssembly.Resources.ServiceAttribute_1.Text
                },
                ReferenceAssemblies = new ReferenceAssemblies(
                    "net8.0",
                    new PackageIdentity(
                        "Microsoft.NETCore.App.Ref", "8.0.0"),
                        Path.Combine("ref", "net8.0"))
                    .AddPackages(ImmutableArray.Create(
                        new PackageIdentity("Microsoft.Extensions.DependencyInjection", "8.0.0")))
            },
        }.WithPreprocessorSymbols();

        await test.RunAsync();
    }

    [Fact]
    public async Task WarnIfAddServicesMissing()
    {
        var test = new AnalyzerTest
        {
            TestCode = """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            public record Command;
            
            public static class Program
            {
                public static void Main()
                {
                    var services = new ServiceCollection();
                    {|#0:services.AddSingleton(new Command())|};
                }
            }
            """,
            TestState =
            {
                Sources =
                {
                    ThisAssembly.Resources.AddServicesNoReflectionExtension.Text,
                    ThisAssembly.Resources.ServiceAttribute.Text,
                    ThisAssembly.Resources.ServiceAttribute_1.Text
                },
                ReferenceAssemblies = new ReferenceAssemblies(
                    "net8.0",
                    new PackageIdentity(
                        "Microsoft.NETCore.App.Ref", "8.0.0"),
                        Path.Combine("ref", "net8.0"))
                    .AddPackages(ImmutableArray.Create(
                        new PackageIdentity("Microsoft.Extensions.DependencyInjection", "8.0.0")))
            },
        }.WithPreprocessorSymbols();

        var expected = Verifier.Diagnostic(AddServicesAnalyzer.NoAddServicesCall).WithLocation(0);
        test.ExpectedDiagnostics.Add(expected);

        await test.RunAsync();
    }

    [Fact]
    public async Task WarnIfAddServicesMissingMultipleLocations()
    {
        var test = new AnalyzerTest
        {
            // Make sure compilation defines the constant/symbol 'DDI_ADDSERVICES'
            // so the generator can detect the presence of the extension method.
            OptionsTransforms = {
                (options) => options
            },
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestCode = """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            public record First;
            public record Second;
            
            public static class Program
            {
                public static void Main()
                {
                    var services = new ServiceCollection();
                    {|#0:services.AddSingleton(new First())|};
                    services.AddSingleton(new Second());
                }
            }
            """,
            TestState =
            {
                Sources =
                {
                    ThisAssembly.Resources.AddServicesNoReflectionExtension.Text,
                    ThisAssembly.Resources.ServiceAttribute.Text,
                    ThisAssembly.Resources.ServiceAttribute_1.Text
                },
                ReferenceAssemblies = new ReferenceAssemblies(
                    "net8.0",
                    new PackageIdentity(
                        "Microsoft.NETCore.App.Ref", "8.0.0"),
                        Path.Combine("ref", "net8.0"))
                    .AddPackages(ImmutableArray.Create(
                        new PackageIdentity("Microsoft.Extensions.DependencyInjection", "8.0.0")))
            },
        }.WithPreprocessorSymbols();

        var expected = Verifier.Diagnostic(AddServicesAnalyzer.NoAddServicesCall).WithLocation(0);
        test.ExpectedDiagnostics.Add(expected);

        await test.RunAsync();
    }

    class GeneratorsTest : CSharpSourceGeneratorTest<IncrementalGenerator, DefaultVerifier>
    {
        //protected override IEnumerable<Type> GetSourceGenerators() => base.GetSourceGenerators().Concat([typeof(IncrementalGenerator)]);
    }
}
