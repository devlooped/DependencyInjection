using System.Collections.Immutable;
using System.IO;
using System.Threading.Tasks;
using Devlooped.Extensions.DependencyInjection;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Xunit.Abstractions;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<Devlooped.Extensions.DependencyInjection.AddServicesAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Tests.CodeAnalysis;

public class DecorateGeneratorTests(ITestOutputHelper Output)
{
    [Fact]
    public async Task ErrorIfDecoratorIsNotService()
    {
        var test = CreateTest(
            """
            using Microsoft.Extensions.DependencyInjection;

            public interface IFoo { }

            [Service]
            public class Foo : IFoo { }

            public class FooDecorator(IFoo inner) : IFoo { }

            public static class Program
            {
                public static void Main()
                {
                    var services = new ServiceCollection();
                    services.AddServices();
                    {|#0:services.Decorate<IFoo, FooDecorator>()|};
                }
            }
            """);

        test.ExpectedDiagnostics.Add(
            Verifier.Diagnostic(IncrementalGenerator.DecoratorMustBeService)
                .WithLocation(0)
                .WithArguments("FooDecorator"));

        await test.RunAsync();
    }

    [Fact]
    public async Task ErrorIfDecoratorLifetimeIsIncompatible()
    {
        var test = CreateTest(
            """
            using Microsoft.Extensions.DependencyInjection;

            public interface IFoo { }

            [Service(ServiceLifetime.Scoped)]
            public class Foo : IFoo { }

            [Service(ServiceLifetime.Singleton)]
            public class FooDecorator(IFoo inner) : IFoo { }

            public static class Program
            {
                public static void Main()
                {
                    var services = new ServiceCollection();
                    services.AddServices();
                    {|#0:services.Decorate<IFoo, FooDecorator>()|};
                }
            }
            """);

        test.ExpectedDiagnostics.Add(
            Verifier.Diagnostic(IncrementalGenerator.DecoratorLifetimeIncompatible)
                .WithLocation(0)
                .WithArguments("FooDecorator", "Singleton", "IFoo", "Scoped"));

        await test.RunAsync();
    }

    [Fact]
    public async Task ErrorIfDecoratorConstructorDoesNotAcceptDecoratedService()
    {
        var test = CreateTest(
            """
            using Microsoft.Extensions.DependencyInjection;

            public interface IFoo { }

            [Service]
            public class Foo : IFoo { }

            [Service]
            public class FooDecorator : IFoo { }

            public static class Program
            {
                public static void Main()
                {
                    var services = new ServiceCollection();
                    services.AddServices();
                    {|#0:services.Decorate<IFoo, FooDecorator>()|};
                }
            }
            """);

        test.ExpectedDiagnostics.Add(
            Verifier.Diagnostic(IncrementalGenerator.DecoratorConstructorMissing)
                .WithLocation(0)
                .WithArguments("FooDecorator", "IFoo"));

        await test.RunAsync();
    }

    static CSharpSourceGeneratorTest<IncrementalGenerator, DefaultVerifier> CreateTest(string source)
    {
        return new CSharpSourceGeneratorTest<IncrementalGenerator, DefaultVerifier>
        {
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestCode = source,
            TestState =
            {
                AnalyzerConfigFiles =
                {
                    ("/.editorconfig",
                    """
                    is_global = true
                    build_property.AddServicesExtension = true
                    """)
                },
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
    }
}
