![Icon](https://raw.githubusercontent.com/devlooped/DependencyInjection/main/assets/img/icon-32.png) .NET DependencyInjection via conventions or [Service] attributes  
============

[![Version](https://img.shields.io/nuget/vpre/Devlooped.Extensions.DependencyInjection.svg)](https://www.nuget.org/packages/Devlooped.Extensions.DependencyInjection)
[![Downloads](https://img.shields.io/nuget/dt/Devlooped.Extensions.DependencyInjection.svg)](https://www.nuget.org/packages/Devlooped.Extensions.DependencyInjection)
[![License](https://img.shields.io/github/license/devlooped/DependencyInjection.svg?color=blue)](https://github.com//devlooped/DependencyInjection/blob/main/license.txt)
[![Build](https://github.com/devlooped/DependencyInjection/actions/workflows/build.yml/badge.svg)](https://github.com/devlooped/DependencyInjection/actions/workflows/build.yml)

<!-- include https://github.com/devlooped/.github/raw/main/sponsorlinkr.md -->
*This project uses [SponsorLink](https://github.com/devlooped#sponsorlink) to attribute sponsor status (direct, indirect or implicit).*
*For IDE usage, sponsor status is required. IDE-only warnings will be issued after a grace period otherwise.*

<!-- https://github.com/devlooped/.github/raw/main/sponsorlinkr.md -->

<!-- #content -->

Automatic compile-time service registrations for Microsoft.Extensions.DependencyInjection with no run-time dependencies, 
from conventions or attributes.

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

> NOTE: The attribute is matched by simple name, so you can define your own attribute 
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

> NOTE: the service is available automatically for the scoped request, because 
> we called the generated `AddServices` that registers the discovered services. 

And that's it. The source generator will discover annotated types in the current 
project and all its references too. Since the registration code is generated at 
compile-time, there is no run-time reflection (or dependencies) whatsoever.

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

> NOTE: you can inspect the generated code by setting `EmitCompilerGeneratedFiles=true` 
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

> NOTE: since the constructor arguments are only used by the source generation to 
> detemine the registration style (and key), but never at run-time, you don't even need 
> to keep it around in a field or property!

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
[![Clarius Org](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/clarius.png "Clarius Org")](https://github.com/clarius)
[![MFB Technologies, Inc.](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/MFB-Technologies-Inc.png "MFB Technologies, Inc.")](https://github.com/MFB-Technologies-Inc)
[![Torutek](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/torutek-gh.png "Torutek")](https://github.com/torutek-gh)
[![DRIVE.NET, Inc.](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/drivenet.png "DRIVE.NET, Inc.")](https://github.com/drivenet)
[![Keith Pickford](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/Keflon.png "Keith Pickford")](https://github.com/Keflon)
[![Thomas Bolon](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/tbolon.png "Thomas Bolon")](https://github.com/tbolon)
[![Kori Francis](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/kfrancis.png "Kori Francis")](https://github.com/kfrancis)
[![Toni Wenzel](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/twenzel.png "Toni Wenzel")](https://github.com/twenzel)
[![Uno Platform](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/unoplatform.png "Uno Platform")](https://github.com/unoplatform)
[![Dan Siegel](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/dansiegel.png "Dan Siegel")](https://github.com/dansiegel)
[![Reuben Swartz](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/rbnswartz.png "Reuben Swartz")](https://github.com/rbnswartz)
[![Jacob Foshee](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/jfoshee.png "Jacob Foshee")](https://github.com/jfoshee)
[![](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/Mrxx99.png "")](https://github.com/Mrxx99)
[![Eric Johnson](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/eajhnsn1.png "Eric Johnson")](https://github.com/eajhnsn1)
[![Ix Technologies B.V.](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/IxTechnologies.png "Ix Technologies B.V.")](https://github.com/IxTechnologies)
[![David JENNI](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/davidjenni.png "David JENNI")](https://github.com/davidjenni)
[![Jonathan ](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/Jonathan-Hickey.png "Jonathan ")](https://github.com/Jonathan-Hickey)
[![Charley Wu](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/akunzai.png "Charley Wu")](https://github.com/akunzai)
[![Jakob Tikjøb Andersen](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/jakobt.png "Jakob Tikjøb Andersen")](https://github.com/jakobt)
[![Tino Hager](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/tinohager.png "Tino Hager")](https://github.com/tinohager)
[![Ken Bonny](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/KenBonny.png "Ken Bonny")](https://github.com/KenBonny)
[![Simon Cropp](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/SimonCropp.png "Simon Cropp")](https://github.com/SimonCropp)
[![agileworks-eu](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/agileworks-eu.png "agileworks-eu")](https://github.com/agileworks-eu)
[![sorahex](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/sorahex.png "sorahex")](https://github.com/sorahex)
[![Zheyu Shen](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/arsdragonfly.png "Zheyu Shen")](https://github.com/arsdragonfly)
[![Vezel](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/vezel-dev.png "Vezel")](https://github.com/vezel-dev)
[![ChilliCream](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/ChilliCream.png "ChilliCream")](https://github.com/ChilliCream)
[![4OTC](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/4OTC.png "4OTC")](https://github.com/4OTC)
[![Vincent Limo](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/v-limo.png "Vincent Limo")](https://github.com/v-limo)
[![Jordan S. Jones](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/jordansjones.png "Jordan S. Jones")](https://github.com/jordansjones)
[![domischell](https://raw.githubusercontent.com/devlooped/sponsors/main/.github/avatars/DominicSchell.png "domischell")](https://github.com/DominicSchell)


<!-- sponsors.md -->

[![Sponsor this project](https://raw.githubusercontent.com/devlooped/sponsors/main/sponsor.png "Sponsor this project")](https://github.com/sponsors/devlooped)
&nbsp;

[Learn more about GitHub Sponsors](https://github.com/sponsors)

<!-- https://github.com/devlooped/sponsors/raw/main/footer.md -->
