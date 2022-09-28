﻿using System.Diagnostics;
using Microsoft.CodeAnalysis;

namespace Devlooped.Extensions.DependencyInjection.Attributed;

[Generator(LanguageNames.CSharp)]
class StaticGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForPostInitialization(c =>
        {
            c.AddSource("ServiceAttribute.g",
                """
                using System;

                namespace Microsoft.Extensions.DependencyInjection
                {
                    /// <summary>
                    /// Configures the registration of a service in an <see cref="IServiceCollection"/>.
                    /// </summary>
                    [AttributeUsage(AttributeTargets.Class)]
                    partial class ServiceAttribute : Attribute
                    {
                        /// <summary>
                        /// Annotates the service with the lifetime.
                        /// </summary>
                        public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Singleton)
                            => Lifetime = lifetime;

                        /// <summary>
                        /// <see cref="ServiceLifetime"/> associated with a registered service 
                        /// in an <see cref="IServiceCollection"/>.
                        /// </summary>
                        public ServiceLifetime Lifetime { get; }
                    }
                }
                """);
        });
    }

    public void Execute(GeneratorExecutionContext context)
    {
        var rootNs = context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.AddServicesNamespace", out var value) && !string.IsNullOrEmpty(value)
            ? value
            : "Microsoft.Extensions.DependencyInjection";

        var className = context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.AddServicesClassName", out value) && !string.IsNullOrEmpty(value) ?
            value : "AddServicesExtension";

        context.AddSource("AddServicesExtension.g",
          $$"""
            using System.ComponentModel;
            using Microsoft.Extensions.DependencyInjection;

            namespace {{rootNs}}
            {
                /// <summary>
                /// Contains the <see cref="AddServices(IServiceCollection)"/> extension methods to register 
                /// compile-time discovered services to an <see cref="IServiceCollection"/>.
                /// </summary>
                [EditorBrowsable(EditorBrowsableState.Never)]
                static partial class {{className}}
                {
                    /// <summary>
                    /// Adds the automatically discovered services that were annotated with a <see cref="ServiceAttribute"/>.
                    /// </summary>
                    /// <param name="services">The <see cref="IServiceCollection"/> to add the services to.</param>
                    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
                    public static IServiceCollection AddServices(this IServiceCollection services)
                    {
                        AddScopedServices(services);
                        AddSingletonServices(services);
                        AddTransientServices(services);

                        return services;
                    }

                    /// <summary>
                    /// Adds discovered scoped services to the collection.
                    /// </summary>
                    static partial void AddScopedServices(IServiceCollection services);
                        
                    /// <summary>
                    /// Adds discovered singleton services to the collection.
                    /// </summary>
                    static partial void AddSingletonServices(IServiceCollection services);

                    /// <summary>
                    /// Adds discovered transient services to the collection.
                    /// </summary>
                    static partial void AddTransientServices(IServiceCollection services);
                }
            }
            """);
    }
}
