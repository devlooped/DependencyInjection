![Icon](https://raw.githubusercontent.com/devlooped/DependencyInjection/main/assets/img/icon-32.png) .NET DependencyInjection via conventions or [Service] attributes  
============

[![EULA](https://img.shields.io/badge/EULA-OSMF-blue?labelColor=black&color=C9FF30)](osmfeula.txt)
[![OSS](https://img.shields.io/github/license/devlooped/oss.svg?color=blue)](license.txt) 
[![Version](https://img.shields.io/nuget/vpre/Devlooped.Extensions.DependencyInjection.svg?color=royalblue)](https://www.nuget.org/packages/Devlooped.Extensions.DependencyInjection)
[![Downloads](https://img.shields.io/nuget/dt/Devlooped.Extensions.DependencyInjection.svg?color=darkmagenta)](https://www.nuget.org/packages/Devlooped.Extensions.DependencyInjection)

Automatic compile-time service registrations for Microsoft.Extensions.DependencyInjection with no run-time dependencies, 
from conventions or attributes.

<!-- include https://github.com/devlooped/.github/raw/main/osmf.md -->
## Open Source Maintenance Fee

To ensure the long-term sustainability of this project, users of this package who generate 
revenue must pay an [Open Source Maintenance Fee](https://opensourcemaintenancefee.org). 
While the source code is freely available under the terms of the [License](license.txt), 
this package and other aspects of the project require [adherence to the Maintenance Fee](osmfeula.txt).

To pay the Maintenance Fee, [become a Sponsor](https://github.com/sponsors/devlooped) at the proper 
OSMF tier. A single fee covers all of [Devlooped packages](https://www.nuget.org/profiles/Devlooped).

<!-- https://github.com/devlooped/.github/raw/main/osmf.md -->
<!-- #content -->

## Usage

The package supports two complementary ways to register services in the DI container, both of which are source-generated at compile-time 
and therefore have no run-time dependencies or reflection overhead:

- **Attribute-based**: annotate your services with `[Service]` or `[Service<TKey>]` attributes to register them in the DI container.
- **Convention-based**: register services by type or name using a convention-based approach.

### Attribute-based

The `[Service(ServiceLifetime)]` attribute is available to explicitly annotate types for registration:

```csharp
[Service(ServiceLifetime.Scoped)]
public class MyService : IMyService, IDisposable
{
    public string Message => "Hello World";

    public void Dispose() { }
}

public interface IMyService 
{
    string Message { get; }
}
```

The `ServiceLifetime` argument is optional and defaults to [ServiceLifetime.Singleton](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.servicelifetime?#fields).

> [!NOTE]
> The attribute is matched by simple name, so you can define your own attribute 
> in your own assembly. It only has to provide a constructor receiving a 
> [ServiceLifetime](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.servicelifetime) argument, 
> and optionally an overload receiving an `object key` for keyed services.

A source generator will emit (at compile-time) an `AddServices` extension method for 
[IServiceCollection](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection) 
which you can call from your startup code that sets up your services, like:

```csharp
var builder = WebApplication.CreateBuilder(args);

// NOTE: **Adds discovered services to the container**
builder.Services.AddServices();
// ...

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGet("/", (IMyService service) => service.Message);

// ...
app.Run();
```

> [!NOTE]
> The service is available automatically for the scoped request, because 
> we called the generated `AddServices` that registers the discovered services. 

And that's it. The source generator will discover annotated types in the current 
project and all its references too. Since the registration code is generated at 
compile-time, there is no run-time reflection (or dependencies) whatsoever.

If the service implements many interfaces and you want to register it only for 
a specific one, you can specify that as the generic argument:

```csharp
[Service<IMyService>(ServiceLifetime.Scoped)]
public class MyService : IMyService, IDisposable
```

> [!TIP]
> If no specific interface is provided, all implemented interfaces are registered 
> for the same service implementation (and they all resolve to the same instance, 
> except for transient lifetime).

### Convention-based

You can also avoid attributes entirely by using a convention-based approach, which 
is nevertheless still compile-time checked and source-generated. This allows 
registering services for which you don't even have the source code to annotate:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServices(typeof(IRepository), ServiceLifetime.Scoped);
// ...
```

This will register all types in the current project and its references that are 
assignable to `IRepository`, with the specified lifetime.

You can also use a regular expression to match services by name instead:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServices(".*Service$");  // defaults to ServiceLifetime.Singleton
// ...
```

You can use a combination of both, as needed. In all cases, NO run-time reflection is 
ever performed, and the compile-time source generator will evaluate the types that are 
assignable to the given type or matching full type names and emit the typed registrations 
as needed.

### Keyed Services

[Keyed services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection?view=aspnetcore-8.0#keyed-services) 
are also supported by providing a key with the `[Service]` attribute. For example:

```csharp
public interface INotificationService
{
    string Notify(string message);
}

[Service("sms")]
public class SmsNotificationService : INotificationService
{
    public string Notify(string message) => $"[SMS] {message}";
}

[Service("email")]
[Service("default")]
public class EmailNotificationService : INotificationService
{
    public string Notify(string message) => $"[Email] {message}";
}
```

Services that want to consume a specific keyed service can use the 
`[FromKeyedServices(object key)]` attribute to specify the key, like:

```csharp
[Service]
public class SmsService([FromKeyedServices("sms")] INotificationService sms)
{
    public void DoSomething() => sms.Notify("Hello");
}
```

In this case, when resolving the `SmsService` from the service provider, the 
right `INotificationService` will be injected, based on the key provided.

Note you can also register the same service using multiple keys, as shown in the 
`EmailNotificationService` above.

> [!IMPORTANT]
> Keyed services are a feature of version 8.0+ of Microsoft.Extensions.DependencyInjection

## How It Works

In all cases, the generated code that implements the registration looks like the following:

```csharp
static partial class AddServicesExtension
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.TryAddScoped(s => new MyService());
        services.AddScoped<IMyService>(s => s.GetRequiredService<MyService>());
        services.AddScoped<IDisposable>(s => s.GetRequiredService<MyService>());
        
        return services;
    }
```

Note how the service is registered as scoped with its own type first, and the 
other two registrations just retrieve the same service (according to its defined 
lifetime). This means the instance is reused and properly registered under 
all implemented interfaces automatically.

> [!TIP]
> You can inspect the generated code by setting `EmitCompilerGeneratedFiles=true` 
> in your project file and browsing the `generated` subfolder under `obj`.

If the service type has dependencies, they will be resolved from the service 
provider by the implementation factory too, like:

```csharp
services.TryAddScoped(s => new MyService(s.GetRequiredService<IMyDependency>(), ...));
```

Keyed services will emit TryAddKeyedXXX methods instead.

## MEF Compatibility

Given the (more or less broad?) adoption of 
[MEF attribute](https://learn.microsoft.com/en-us/dotnet/framework/mef/attributed-programming-model-overview-mef)
(whether [.NET MEF, NuGet MEF or VS MEF](https://github.com/microsoft/vs-mef/blob/main/doc/mef_library_differences.md)) in .NET, 
the generator also supports the `[Export]` attribute to denote a service (the 
type argument as well as contract name are ignored, since those aren't supported 
in the DI container). 

In order to specify a singleton (shared) instance in MEF, you have to annotate the 
type with an extra attribute: `[Shared]` in NuGet MEF (from [System.Composition](http://nuget.org/packages/System.Composition.AttributedModel)) 
or `[PartCreationPolicy(CreationPolicy.Shared)]` in .NET MEF 
(from [System.ComponentModel.Composition](https://www.nuget.org/packages/System.ComponentModel.Composition)).

Both `[Export("contractName")]` and `[Import("contractName")]` are supported and 
will be used to register and resolve keyed services respectively, meaning you can 
typically depend on just `[Export]` and `[Import]` attributes for all your DI 
annotations and have them work automatically when composed in the DI container.

## Advanced Scenarios

### `Lazy<T>` and `Func<T>` Dependencies

A `Lazy<T>` for each interface (and main implementation) is automatically provided 
too, so you can take a lazy dependency out of the box too. In this case, the lifetime 
of the dependency `T` becomes tied to the lifetime of the component taking the lazy 
dependency, for obvious reasons. The `Lazy<T>` is merely a lazy resolving of the 
dependency via the service provider. The lazy itself isn't costly to construct, and 
since the lifetime of the underlying service, plus the lifetime of the consuming 
service determine the ultimate lifetime of the lazy, no additional configuration is 
necessary for it, as it's always registered as a transient component. Generated code 
looks like the following:

```csharp
services.AddTransient(s => new Lazy<IMyService>(s.GetRequiredService<MyService>));
```

A `Func<T>` is also automatically registered, but it is just a delegate to the 
actual `IServiceProvider.GetRequiredService<T>`. Generated code looks like the 
following:


```csharp
services.AddTransient<Func<IMyService>>(s => s.GetRequiredService<MyService>);
```

Repeatedly invoking the function will result in an instance of the required 
service that depends on the registered lifetime for it. If it was registered 
as a singleton, for example, you would get the same value every time, just 
as if you had used a dependency of `Lazy<T>` instead, but invoking the 
service provider each time, instead of only once. This makes this pattern 
more useful for transient services that you intend to use for a short time 
(and potentially dispose afterwards).


### Your Own ServiceAttribute

If you want to declare your own `ServiceAttribute` and reuse from your projects, 
so as to avoid taking a (development-only, compile-time only) dependency on this 
package from your library projects, you can just declare it like so:

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class ServiceAttribute : Attribute
{
    public ServiceAttribute(ServiceLifetime lifetime = ServiceLifetime.Singleton) { }
    public ServiceAttribute(object key, ServiceLifetime lifetime = ServiceLifetime.Singleton) { }
}
```


> [!TIP]
> Since the constructor arguments are only used by the source generation to 
> detemine the registration style (and key), but never at run-time, you don't even need 
> to keep them around in a field or property!

With this in place, you only need to add this package to the top-level project 
that is adding the services to the collection!

The attribute is matched by simple name, so it can exist in any namespace. 

If you want to avoid adding the attribute to the project referencing this package, 
set the `$(AddServiceAttribute)` to `false` via MSBuild:

```xml
<PropertyGroup>
  <AddServiceAttribute>false</AddServiceAttribute>
</PropertyGroup>
```

If you want to avoid generating the `AddServices` extension method to the project referencing 
this package, set the `$(AddServicesExtension)` to `false` via MSBuild:

```xml
<PropertyGroup>
  <AddServicesExtension>false</AddServicesExtension>
</PropertyGroup>
```

### Choose Constructor

If you want to choose a specific constructor to be used for the service implementation 
factory registration (instead of the default one which will be the one with the most 
parameters), you can annotate it with `[ImportingConstructor]` from either NuGet MEF 
([System.Composition](http://nuget.org/packages/System.Composition.AttributedModel)) 
or .NET MEF ([System.ComponentModel.Composition](https://www.nuget.org/packages/System.ComponentModel.Composition)).

<!-- #content -->

# Dogfooding

[![CI Version](https://img.shields.io/endpoint?url=https://shields.kzu.app/vpre/Devlooped.Extensions.DependencyInjection/main&label=nuget.ci&color=brightgreen)](https://pkg.kzu.app/index.json)
[![Build](https://github.com/devlooped/DependencyInjection/actions/workflows/build.yml/badge.svg)](https://github.com/devlooped/DependencyInjection/actions/workflows/build.yml)

We also produce CI packages from branches and pull requests so you can dogfood builds as quickly as they are produced. 

The CI feed is `https://pkg.kzu.app/index.json`. 

The versioning scheme for packages is:

- PR builds: *42.42.42-pr*`[NUMBER]`
- Branch builds: *42.42.42-*`[BRANCH]`.`[COMMITS]`


<!-- include https://github.com/devlooped/sponsors/raw/main/footer.md -->
# Sponsors 

<!-- sponsors.md -->
[![Clarius Org](https://avatars.githubusercontent.com/u/71888636?v=4&s=39 "Clarius Org")](https://github.com/clarius)
[![MFB Technologies, Inc.](https://avatars.githubusercontent.com/u/87181630?v=4&s=39 "MFB Technologies, Inc.")](https://github.com/MFB-Technologies-Inc)
[![SandRock](https://avatars.githubusercontent.com/u/321868?u=99e50a714276c43ae820632f1da88cb71632ec97&v=4&s=39 "SandRock")](https://github.com/sandrock)
[![DRIVE.NET, Inc.](https://avatars.githubusercontent.com/u/15047123?v=4&s=39 "DRIVE.NET, Inc.")](https://github.com/drivenet)
[![Keith Pickford](https://avatars.githubusercontent.com/u/16598898?u=64416b80caf7092a885f60bb31612270bffc9598&v=4&s=39 "Keith Pickford")](https://github.com/Keflon)
[![Thomas Bolon](https://avatars.githubusercontent.com/u/127185?u=7f50babfc888675e37feb80851a4e9708f573386&v=4&s=39 "Thomas Bolon")](https://github.com/tbolon)
[![Kori Francis](https://avatars.githubusercontent.com/u/67574?u=3991fb983e1c399edf39aebc00a9f9cd425703bd&v=4&s=39 "Kori Francis")](https://github.com/kfrancis)
[![Uno Platform](https://avatars.githubusercontent.com/u/52228309?v=4&s=39 "Uno Platform")](https://github.com/unoplatform)
[![Reuben Swartz](https://avatars.githubusercontent.com/u/724704?u=2076fe336f9f6ad678009f1595cbea434b0c5a41&v=4&s=39 "Reuben Swartz")](https://github.com/rbnswartz)
[![Jacob Foshee](https://avatars.githubusercontent.com/u/480334?v=4&s=39 "Jacob Foshee")](https://github.com/jfoshee)
[![](https://avatars.githubusercontent.com/u/33566379?u=bf62e2b46435a267fa246a64537870fd2449410f&v=4&s=39 "")](https://github.com/Mrxx99)
[![Eric Johnson](https://avatars.githubusercontent.com/u/26369281?u=41b560c2bc493149b32d384b960e0948c78767ab&v=4&s=39 "Eric Johnson")](https://github.com/eajhnsn1)
[![David JENNI](https://avatars.githubusercontent.com/u/3200210?v=4&s=39 "David JENNI")](https://github.com/davidjenni)
[![Jonathan ](https://avatars.githubusercontent.com/u/5510103?u=98dcfbef3f32de629d30f1f418a095bf09e14891&v=4&s=39 "Jonathan ")](https://github.com/Jonathan-Hickey)
[![Charley Wu](https://avatars.githubusercontent.com/u/574719?u=ea7c743490c83e8e4b36af76000f2c71f75d636e&v=4&s=39 "Charley Wu")](https://github.com/akunzai)
[![Ken Bonny](https://avatars.githubusercontent.com/u/6417376?u=569af445b6f387917029ffb5129e9cf9f6f68421&v=4&s=39 "Ken Bonny")](https://github.com/KenBonny)
[![Simon Cropp](https://avatars.githubusercontent.com/u/122666?v=4&s=39 "Simon Cropp")](https://github.com/SimonCropp)
[![agileworks-eu](https://avatars.githubusercontent.com/u/5989304?v=4&s=39 "agileworks-eu")](https://github.com/agileworks-eu)
[![Zheyu Shen](https://avatars.githubusercontent.com/u/4067473?v=4&s=39 "Zheyu Shen")](https://github.com/arsdragonfly)
[![Vezel](https://avatars.githubusercontent.com/u/87844133?v=4&s=39 "Vezel")](https://github.com/vezel-dev)
[![ChilliCream](https://avatars.githubusercontent.com/u/16239022?v=4&s=39 "ChilliCream")](https://github.com/ChilliCream)
[![4OTC](https://avatars.githubusercontent.com/u/68428092?v=4&s=39 "4OTC")](https://github.com/4OTC)
[![Vincent Limo](https://avatars.githubusercontent.com/devlooped-user?s=39 "Vincent Limo")](https://github.com/v-limo)
[![domischell](https://avatars.githubusercontent.com/u/66068846?u=0a5c5e2e7d90f15ea657bc660f175605935c5bea&v=4&s=39 "domischell")](https://github.com/DominicSchell)
[![Justin Wendlandt](https://avatars.githubusercontent.com/u/1068431?u=f7715ed6a8bf926d96ec286f0f1c65f94bf86928&v=4&s=39 "Justin Wendlandt")](https://github.com/jwendl)
[![Adrian Alonso](https://avatars.githubusercontent.com/u/2027083?u=129cf516d99f5cb2fd0f4a0787a069f3446b7522&v=4&s=39 "Adrian Alonso")](https://github.com/adalon)
[![Michael Hagedorn](https://avatars.githubusercontent.com/u/61711586?u=8f653dfcb641e8c18cc5f78692ebc6bb3a0c92be&v=4&s=39 "Michael Hagedorn")](https://github.com/Eule02)
[![torutek](https://avatars.githubusercontent.com/u/33917059?v=4&s=39 "torutek")](https://github.com/torutek)
[![mccaffers](https://avatars.githubusercontent.com/u/16667079?u=739e110e62a75870c981640447efa5eb2cb3bc8f&v=4&s=39 "mccaffers")](https://github.com/mccaffers)
[![Christoph Hochstätter](https://avatars.githubusercontent.com/u/17645550?u=01bbdcb84d03cac26260f1c951e046d24a324591&v=4&s=39 "Christoph Hochstätter")](https://github.com/christoh)
[![ADS Fund](https://avatars.githubusercontent.com/u/202042116?v=4&s=39 "ADS Fund")](https://github.com/ADS-Fund)


<!-- sponsors.md -->
[![Sponsor this project](https://avatars.githubusercontent.com/devlooped-sponsor?s=118 "Sponsor this project")](https://github.com/sponsors/devlooped)

[Learn more about GitHub Sponsors](https://github.com/sponsors)

<!-- https://github.com/devlooped/sponsors/raw/main/footer.md -->
