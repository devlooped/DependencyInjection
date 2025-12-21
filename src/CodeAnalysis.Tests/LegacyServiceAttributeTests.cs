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
using AnalyzerTest = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerTest<Devlooped.Extensions.DependencyInjection.LegacyServiceAttributeAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;
using Verifier = Microsoft.CodeAnalysis.CSharp.Testing.CSharpAnalyzerVerifier<Devlooped.Extensions.DependencyInjection.LegacyServiceAttributeAnalyzer, Microsoft.CodeAnalysis.Testing.DefaultVerifier>;

namespace Tests.CodeAnalysis;

public class LegacyServiceAttributeTests(ITestOutputHelper Output)
{
    [Fact]
    public async Task ErrorIfTKeyMatchesStringConstant()
    {
        var test = new AnalyzerTest
        {
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestCode =
                """
                using System;
                using Microsoft.Extensions.DependencyInjection;
    
                [{|#0:Service<string>("my")|}]
                public class MyService { }
                """
        }.WithTestState();

        var expected = Verifier.Diagnostic(LegacyServiceAttributeAnalyzer.ServiceTypeNotKeyType).WithLocation(0);
        test.ExpectedDiagnostics.Add(expected);

        await test.RunAsync();
    }

    [Fact]
    public async Task ExecuteSyncWithAsyncCommand()
    {
        var test = new CSharpCodeFixTest<LegacyServiceAttributeAnalyzer, LegacyServiceAttributeFixer, DefaultVerifier>
        {
            TestBehaviors = TestBehaviors.SkipGeneratedSourcesCheck,
            TestCode =
                """
                using System;
                using Microsoft.Extensions.DependencyInjection;
    
                [{|#0:Service<string>("my")|}]
                public class MyService { }
                """,
            FixedCode =
                """
                using System;
                using Microsoft.Extensions.DependencyInjection;
    
                [Service("my")]
                public class MyService { }
                """,
        }.WithTestState();

        test.ExpectedDiagnostics.Add(new DiagnosticResult(LegacyServiceAttributeAnalyzer.ServiceTypeNotKeyType).WithLocation(0));
        test.FixedState.Sources.AddStaticFiles();

        await test.RunAsync();
    }
}