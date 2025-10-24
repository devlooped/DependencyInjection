using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Devlooped.Extensions.DependencyInjection;

[Generator(LanguageNames.CSharp)]
public class StaticGenerator : ISourceGenerator
{
    public const string DefaultNamespace = "Microsoft.Extensions.DependencyInjection";
    public const string DefaultAddServicesClass = nameof(AddServicesNoReflectionExtension);

    public static string AddServicesExtension => ThisAssembly.Resources.AddServicesNoReflectionExtension.Text;
    public static string ServiceAttribute => ThisAssembly.Resources.ServiceAttribute.Text;
    public static string ServiceAttributeT => ThisAssembly.Resources.ServiceAttribute_1.Text;

    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForPostInitialization(c =>
        {
            c.AddSource("ServiceAttribute.g", ThisAssembly.Resources.ServiceAttribute.Text);
            c.AddSource("ServiceAttribute`1.g", ThisAssembly.Resources.ServiceAttribute_1.Text);
        });
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var rootNs = context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.AddServicesNamespace", out var value) && !string.IsNullOrEmpty(value)
            ? value : DefaultNamespace;

        var className = context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.AddServicesClassName", out value) && !string.IsNullOrEmpty(value) ?
            value : DefaultAddServicesClass;

        context.AddSource(DefaultAddServicesClass + ".g", ThisAssembly.Resources.AddServicesNoReflectionExtension.Text
                .Replace("namespace " + DefaultNamespace, "namespace " + rootNs)
                .Replace(DefaultAddServicesClass, className));
    }
}