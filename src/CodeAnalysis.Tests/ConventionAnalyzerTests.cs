using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Devlooped.Extensions.DependencyInjection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using Xunit.Abstractions;
using AnalyzerTest = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<Devlooped.Extensions.DependencyInjection.ConventionsAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<Devlooped.Extensions.DependencyInjection.ConventionsAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Tests.CodeAnalysis;

public class ConventionAnalyzerTests(ITestOutputHelper Output)
{
    [Fact]
    public async Task ErrorIfNonTypeOf()
    {
        var test = new AnalyzerTest
        {
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestCode =
            """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            public static class Program
            {
                public static void Main()
                {
                    var services = new ServiceCollection();
                    var type = typeof(IDisposable);
                    services.AddServices({|#0:type|});
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
        };

        var expected = Verifier.Diagnostic(ConventionsAnalyzer.AssignableTypeOfRequired).WithLocation(0);
        test.ExpectedDiagnostics.Add(expected);

        await test.RunAsync();
    }

    [Fact]
    public async Task NoErrorOnTypeOfAndLifetime()
    {
        var test = new AnalyzerTest
        {
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestCode =
            """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            public static class Program
            {
                public static void Main()
                {
                    var services = new ServiceCollection();
                    services.AddServices(typeof(IDisposable), ServiceLifetime.Scoped);
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
        };

        //var expected = Verifier.Diagnostic(ConventionsAnalyzer.AssignableTypeOfRequired).WithLocation(0);
        //test.ExpectedDiagnostics.Add(expected);

        await test.RunAsync();
    }

    [Fact]
    public async Task WarnIfOpenGeneric()
    {
        var test = new AnalyzerTest
        {
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestCode =
            """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            public interface IRepository<T> { }
            public class Repository<T> : IRepository<T> { }
            
            public static class Program
            {
                public static void Main()
                {
                    var services = new ServiceCollection();
                    services.AddServices({|#0:typeof(Repository<>)|}, ServiceLifetime.Scoped);
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
        };

        var expected = Verifier.Diagnostic(ConventionsAnalyzer.OpenGenericType).WithLocation(0);
        test.ExpectedDiagnostics.Add(expected);

        await test.RunAsync();
    }

    [Fact]
    public async Task WarnIfAmbiguousLifetime()
    {
        var test = new CSharpSourceGeneratorTest<IncrementalGenerator, DefaultVerifier>
        {
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestCode =
            """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            public interface IRepository { }
            public class MyRepository : IRepository { }
            
            public static class Program
            {
                public static void Main()
                {
                    var services = new ServiceCollection();
                    {|#0:services.AddServices(typeof(IRepository), ServiceLifetime.Scoped)|};
                    {|#1:services.AddServices("Repository", ServiceLifetime.Singleton)|};
                }
            }
            """,
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
        };

        var expected = Verifier.Diagnostic(IncrementalGenerator.AmbiguousLifetime)
            .WithArguments("MyRepository", "Scoped, Singleton")
            .WithLocation(0).WithLocation(1);

        test.ExpectedDiagnostics.Add(expected);

        await test.RunAsync();
    }

    [Fact]
    public async Task NoWarnIfMultipleSameLifetime()
    {
        var test = new CSharpSourceGeneratorTest<IncrementalGenerator, DefaultVerifier>
        {
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestCode =
            """
            using System;
            using Microsoft.Extensions.DependencyInjection;
            
            public interface IRepository { }

            [Service]
            public class MyRepository : IRepository { }
            
            public static class Program
            {
                public static void Main()
                {
                    var services = new ServiceCollection();
                    services.AddServices(typeof(IRepository));
                }
            }
            """,
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
        };

        await test.RunAsync();
    }
}