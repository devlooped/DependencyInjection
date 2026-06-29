using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.DependencyInjection;

public class GenerationTests(ITestOutputHelper Output)
{
    [Fact]
    public void RegisterInternalService()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        var instance = services.GetRequiredService<IService>();
    }

    [Fact]
    public void RegisterSingletonService()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        var instance = services.GetRequiredService<SingletonService>();

        Assert.Same(instance, services.GetRequiredService<SingletonService>());
        Assert.Same(instance, services.GetRequiredService<Func<SingletonService>>().Invoke());
        Assert.Same(instance, services.GetRequiredService<Lazy<SingletonService>>().Value);

        Assert.Same(instance, services.GetRequiredService<IFormattable>());
        Assert.Same(instance, services.GetRequiredService<Func<IFormattable>>().Invoke());
        Assert.Same(instance, services.GetRequiredService<Lazy<IFormattable>>().Value);
    }

    [Fact]
    public void RegisterTransientService()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        var instance = services.GetRequiredService<TransientService>();

        Assert.NotSame(instance, services.GetRequiredService<TransientService>());
        Assert.NotSame(instance, services.GetRequiredService<Func<TransientService>>().Invoke());
        Assert.NotSame(instance, services.GetRequiredService<Lazy<TransientService>>().Value);

        Assert.NotSame(instance, services.GetRequiredService<ICloneable>());
        Assert.NotSame(instance, services.GetRequiredService<Func<ICloneable>>().Invoke());
        Assert.NotSame(instance, services.GetRequiredService<Lazy<ICloneable>>().Value);
    }

    [Fact]
    public void RegisterScopedService()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        using var scope = services.CreateScope();

        var instance = scope.ServiceProvider.GetRequiredService<ScopedService>();

        // Within the scope, we get the same instance
        Assert.Same(instance, scope.ServiceProvider.GetRequiredService<ScopedService>());
        Assert.Same(instance, scope.ServiceProvider.GetRequiredService<Func<ScopedService>>().Invoke());
        Assert.Same(instance, scope.ServiceProvider.GetRequiredService<Lazy<ScopedService>>().Value);

        Assert.Same(instance, scope.ServiceProvider.GetRequiredService<IComparable>());
        Assert.Same(instance, scope.ServiceProvider.GetRequiredService<Func<IComparable>>().Invoke());
        Assert.Same(instance, scope.ServiceProvider.GetRequiredService<Lazy<IComparable>>().Value);

        // Outside the scope, we don't
        Assert.NotSame(instance, services.GetRequiredService<IComparable>());
        Assert.NotSame(instance, services.GetRequiredService<Func<IComparable>>().Invoke());
        Assert.NotSame(instance, services.GetRequiredService<Lazy<IComparable>>().Value);
    }

    [Fact]
    public void RegisterKeyedSingletonService()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        var instance = services.GetRequiredKeyedService<KeyedSingletonService>(42);

        Assert.Same(instance, services.GetRequiredKeyedService<KeyedSingletonService>(42));
        Assert.Same(instance, services.GetRequiredKeyedService<Func<KeyedSingletonService>>(42).Invoke());
        Assert.Same(instance, services.GetRequiredKeyedService<Lazy<KeyedSingletonService>>(42).Value);

        Assert.Same(instance, services.GetRequiredKeyedService<IFormattable>(42));
        Assert.Same(instance, services.GetRequiredKeyedService<Func<IFormattable>>(42).Invoke());
        Assert.Same(instance, services.GetRequiredKeyedService<Lazy<IFormattable>>(42).Value);
    }

    [Fact]
    public void RegisterKeyedTransientService()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        var instance = services.GetRequiredKeyedService<KeyedTransientService>(PlatformID.Win32NT);

        Assert.NotSame(instance, services.GetRequiredKeyedService<KeyedTransientService>(PlatformID.Win32NT));
        Assert.NotSame(instance, services.GetRequiredKeyedService<Func<KeyedTransientService>>(PlatformID.Win32NT).Invoke());
        Assert.NotSame(instance, services.GetRequiredKeyedService<Lazy<KeyedTransientService>>(PlatformID.Win32NT).Value);

        Assert.NotSame(instance, services.GetRequiredKeyedService<ICloneable>(PlatformID.Win32NT));
        Assert.NotSame(instance, services.GetRequiredKeyedService<Func<ICloneable>>(PlatformID.Win32NT).Invoke());
        Assert.NotSame(instance, services.GetRequiredKeyedService<Lazy<ICloneable>>(PlatformID.Win32NT).Value);
    }

    [Fact]
    public void RegisterKeyedScopedService()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        using var scope = services.CreateScope();

        var instance = scope.ServiceProvider.GetRequiredKeyedService<KeyedScopedService>("A");

        // Within the scope, we get the same instance
        Assert.Same(instance, scope.ServiceProvider.GetRequiredKeyedService<KeyedScopedService>("A"));
        Assert.Same(instance, scope.ServiceProvider.GetRequiredKeyedService<Func<KeyedScopedService>>("A").Invoke());
        Assert.Same(instance, scope.ServiceProvider.GetRequiredKeyedService<Lazy<KeyedScopedService>>("A").Value);

        Assert.Same(instance, scope.ServiceProvider.GetRequiredKeyedService<IComparable>("A"));
        Assert.Same(instance, scope.ServiceProvider.GetRequiredKeyedService<Func<IComparable>>("A").Invoke());
        Assert.Same(instance, scope.ServiceProvider.GetRequiredKeyedService<Lazy<IComparable>>("A").Value);

        // Outside the scope, we don't
        Assert.NotSame(instance, services.GetRequiredKeyedService<IComparable>("A"));
        Assert.NotSame(instance, services.GetRequiredKeyedService<Func<IComparable>>("A").Invoke());
        Assert.NotSame(instance, services.GetRequiredKeyedService<Lazy<IComparable>>("A").Value);
    }

    [Fact]
    public void ResolvesKeyedDependency()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        var singleton = services.GetRequiredKeyedService<KeyedSingletonService>(42);

        using var scope = services.CreateScope();

        var instance = scope.ServiceProvider.GetRequiredKeyedService<FromKeyedDependency>("FromKeyed");

        // Within the scope, we get the same instance
        Assert.Same(singleton, instance.Dependency);
    }

    [Fact]
    public void ResolvesKeyedTransientDependency()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        using var scope = services.CreateScope();

        var first = scope.ServiceProvider.GetRequiredKeyedService<FromTransientKeyedDependency>("FromKeyedTransient");
        var second = scope.ServiceProvider.GetRequiredKeyedService<FromTransientKeyedDependency>("FromKeyedTransient");

        // Within the scope, we get the same instance
        Assert.NotSame(first, second);
    }

    [Fact]
    public void ResolvesKeyedDependencyForNonKeyed()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        var singleton = services.GetRequiredService<NonKeyedWithKeyedDependency>();

        Assert.NotNull(singleton.Dependency);
    }

    [Fact]
    public void ResolvesKeyedFromContracts()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        var singleton = services.GetRequiredKeyedService<DependencyFromKeyedContract>("service");

        Assert.NotNull(singleton.Dependency);
    }

    [Fact]
    public void ResolveMultipleKeys()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        var sms = services.GetRequiredKeyedService<INotificationService>("sms");
        var email = services.GetRequiredKeyedService<INotificationService>("email");
        var def = services.GetRequiredKeyedService<INotificationService>("default");

        // Each gets its own instance, since we can't tell apart. Lifetimes can also be disparate.
        Assert.NotSame(sms, email);
        Assert.NotSame(sms, def);
        Assert.NotSame(email, def);
    }

    [Fact]
    public void ResolvesDependency()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        var singleton = services.GetRequiredService<SingletonService>();

        using var scope = services.CreateScope();

        var instance = scope.ServiceProvider.GetRequiredService<ScopedService>();

        // Within the scope, we get the same instance
        Assert.Same(singleton, instance.Singleton);
    }

    [Fact]
    public void RetrieveMany()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        var instances = services.GetServices<IDisposable>();

        Assert.Equal(3, instances.Count());
    }

    [Fact]
    public void RegisterWithGenericOutParameterHierarchy()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        Assert.NotNull(services.GetService<IObservable<MyEvent>>());
        Assert.NotNull(services.GetService<IObservable<BaseEvent>>());
        Assert.NotNull(services.GetService<IObservable<IEvent>>());
        Assert.Null(services.GetService<IObservable<object>>());
    }

    [Fact]
    public void RegisterWithCustomServiceAttribute()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        var instance = services.GetRequiredService<MyAttributedService>();

        Assert.Same(instance, services.GetRequiredService<MyAttributedService>());
        Assert.Same(instance, services.GetRequiredService<IAsyncDisposable>());
    }

    [Fact]
    public void RegisterWithSpecificServiceType()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        var services = collection.BuildServiceProvider();

        Assert.NotNull(services.GetRequiredService<ISpecificService>());
        Assert.Null(services.GetService<INonSpecificService>());
    }

    [Fact]
    public void DecorateService()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        collection.Decorate<IDecoratedService, DecoratedServiceDecorator>();
        var services = collection.BuildServiceProvider();

        using var scope = services.CreateScope();

        var instance = Assert.IsType<DecoratedServiceDecorator>(scope.ServiceProvider.GetRequiredService<IDecoratedService>());

        Assert.IsType<DecoratedService>(instance.Inner);
        Assert.Same(instance, scope.ServiceProvider.GetRequiredService<IDecoratedService>());
        Assert.Same(instance, scope.ServiceProvider.GetRequiredService<Func<IDecoratedService>>().Invoke());
        Assert.Same(instance, scope.ServiceProvider.GetRequiredService<Lazy<IDecoratedService>>().Value);
        Assert.Same(services.GetRequiredService<SingletonService>(), instance.Singleton);
    }

    [Fact]
    public void DecorateMultipleRegistrations()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        collection.Decorate<IMultipleDecoratedService, MultipleDecoratedServiceDecorator>();
        var services = collection.BuildServiceProvider();

        var instances = services.GetServices<IMultipleDecoratedService>()
            .Cast<MultipleDecoratedServiceDecorator>()
            .ToList();

        Assert.Equal(2, instances.Count);
        Assert.Contains(instances, x => x.Inner is FirstMultipleDecoratedService);
        Assert.Contains(instances, x => x.Inner is SecondMultipleDecoratedService);
    }

    [Fact]
    public void DecorateMultipleRegistrationsAsIEnumerableDependency()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        collection.Decorate<IMultipleDecoratedService, MultipleDecoratedServiceDecorator>();
        var services = collection.BuildServiceProvider();

        var consumer = services.GetRequiredService<IEnumerableDecoratedConsumer>();

        var decorated = consumer.Services.Cast<MultipleDecoratedServiceDecorator>().ToList();
        Assert.Equal(2, decorated.Count);
        Assert.Contains(decorated, x => x.Inner is FirstMultipleDecoratedService);
        Assert.Contains(decorated, x => x.Inner is SecondMultipleDecoratedService);
    }

    [Fact]
    public void DecorateKeyedService()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        collection.Decorate<IKeyedDecoratedService, KeyedDecoratedServiceDecorator>("decorated");
        var services = collection.BuildServiceProvider();

        var instance = Assert.IsType<KeyedDecoratedServiceDecorator>(
            services.GetRequiredKeyedService<IKeyedDecoratedService>("decorated"));

        Assert.IsType<KeyedDecoratedService>(instance.Inner);
        Assert.Same(services.GetRequiredService<SingletonService>(), instance.Singleton);

        var factory = services.GetRequiredKeyedService<Func<IKeyedDecoratedService>>("decorated");
        Assert.IsType<KeyedDecoratedServiceDecorator>(factory());
        Assert.IsType<OtherKeyedDecoratedService>(services.GetRequiredKeyedService<IKeyedDecoratedService>("other"));
    }

    [Fact]
    public void DecorateThrowsIfDecoratedServiceIsNotRegistered()
    {
        var collection = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            collection.Decorate<IDecoratedService, DecoratedServiceDecorator>());

        Assert.Contains(nameof(IDecoratedService), ex.Message);
    }

    [Fact]
    public void DecorateKeyedThrowsIfDecoratedServiceIsNotRegistered()
    {
        var collection = new ServiceCollection();
        collection.AddServices();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            collection.Decorate<IKeyedDecoratedService, KeyedDecoratedServiceDecorator>("missing"));

        Assert.Contains(nameof(IKeyedDecoratedService), ex.Message);
        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public void DecorateServiceReadmeExample()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        collection.Decorate<IReadmeNotificationService, ReadmeLoggingNotificationService>();
        var services = collection.BuildServiceProvider();

        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;

        var instance = Assert.IsType<ReadmeLoggingNotificationService>(provider.GetRequiredService<IReadmeNotificationService>());
        Assert.IsType<ReadmeEmailNotificationService>(instance.Inner);

        Assert.Same(instance, provider.GetRequiredService<Func<IReadmeNotificationService>>().Invoke());
        Assert.Same(instance, provider.GetRequiredService<Lazy<IReadmeNotificationService>>().Value);
    }

    [Fact]
    public void DecorateWorksWithDecoratorThatHasNoServiceAttribute()
    {
        var collection = new ServiceCollection();
        collection.AddServices();
        collection.Decorate<IUnattributedDecorated, UnattributedDecorator>();
        var services = collection.BuildServiceProvider();

        var instance = Assert.IsType<UnattributedDecorator>(services.GetRequiredService<IUnattributedDecorated>());
        Assert.IsType<UnattributedDecoratedImpl>(instance.Inner);
        Assert.Same(services.GetRequiredService<SingletonService>(), instance.Singleton);

        Assert.Same(instance, services.GetRequiredService<Func<IUnattributedDecorated>>().Invoke());
        Assert.Same(instance, services.GetRequiredService<Lazy<IUnattributedDecorated>>().Value);
    }

    [GenerationTests.Service(ServiceLifetime.Singleton)]
    public class MyAttributedService : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => throw new NotImplementedException();
    }

    class ServiceAttribute : Attribute
    {
        public ServiceAttribute(ServiceLifetime lifetime) { }
    }
}

[Service(ServiceLifetime.Singleton)]
public class SingletonService : IDisposable, IFormattable
{
    public void Dispose() => throw new NotImplementedException();
    public string ToString(string? format, IFormatProvider? formatProvider) => throw new NotImplementedException();
}

[Service(ServiceLifetime.Transient)]
public class TransientService : ICloneable, IDisposable
{
    public object Clone() => throw new NotImplementedException();
    public void Dispose() => throw new NotImplementedException();
}

[Service(ServiceLifetime.Scoped)]
public class ScopedService : IComparable, IDisposable
{
    public ScopedService(SingletonService singleton) => Singleton = singleton;
    // Will not be picked up because it's not accessible from within the assembly
    // If it were internal, it would be the selected one instead (most args)
    protected ScopedService(SingletonService singleton, CancellationTokenSource source) => Singleton = singleton;
    public SingletonService Singleton { get; }
    public int CompareTo(object? obj) => throw new NotImplementedException();
    public void Dispose() { }
}

public interface IEvent { }
public class BaseEvent : IEvent { }
public class MyEvent : BaseEvent { }
[Service]
public class ObservableService : IObservable<MyEvent>
{
    public IDisposable Subscribe(IObserver<MyEvent> observer) => throw new NotImplementedException();
}

[Service(42, ServiceLifetime.Singleton)]
public class KeyedSingletonService : IFormattable
{
    public string ToString(string? format, IFormatProvider? formatProvider) => throw new NotImplementedException();
}

[Service(PlatformID.Win32NT, ServiceLifetime.Transient)]
public class KeyedTransientService : ICloneable
{
    public object Clone() => throw new NotImplementedException();
}

[Service("A", ServiceLifetime.Scoped)]
public class KeyedScopedService : IComparable
{
    public int CompareTo(object? obj) => throw new NotImplementedException();
}

[Service("FromKeyed", ServiceLifetime.Scoped)]
public class FromKeyedDependency([FromKeyedServices(42)] IFormattable dependency)
{
    public IFormattable Dependency => dependency;
}

[Service("FromKeyedTransient", ServiceLifetime.Transient)]
public class FromTransientKeyedDependency([FromKeyedServices(42)] IFormattable dependency)
{
    public IFormattable Dependency => dependency;
}

[Service]
public class NonKeyedWithKeyedDependency([FromKeyedServices(PlatformID.Win32NT)] ICloneable dependency)
{
    public ICloneable Dependency => dependency;
}

[Export("contract")]
public class KeyedByContractName { }

[Export("service")]
public class DependencyFromKeyedContract([Import("contract")] KeyedByContractName dependency)
{
    public KeyedByContractName Dependency => dependency;
}

public interface IService { }
[Service]
class InternalService : IService { }

public interface INotificationService
{
    string Notify(string message);
}

[Service("sms")]
public class SmsNotificationService : INotificationService
{
    public string Notify(string message) => $"[SMS] {message}";
}

// Showcases that legacy generic Service<TKey> attribute still works
// but now with new semantics enforced by an analyzer.
[Service("email")]
[Service<INotificationService>("default")]
public class EmailNotificationService : INotificationService
{
    public string Notify(string message) => $"[Email] {message}";
}

public interface ISpecificService;
public interface INonSpecificService;

[Service<ISpecificService>]
public class SpecificServiceType : ISpecificService, INonSpecificService
{
    public void Dispose() => throw new NotImplementedException();
}

public interface IDecoratedService { }

[Service(ServiceLifetime.Scoped)]
public class DecoratedService : IDecoratedService { }

[Service(ServiceLifetime.Scoped)]
public class DecoratedServiceDecorator(IDecoratedService inner, SingletonService singleton) : IDecoratedService
{
    public IDecoratedService Inner => inner;
    public SingletonService Singleton => singleton;
}

public interface IMultipleDecoratedService { }

[Service(ServiceLifetime.Transient)]
public class FirstMultipleDecoratedService : IMultipleDecoratedService { }

[Service(ServiceLifetime.Transient)]
public class SecondMultipleDecoratedService : IMultipleDecoratedService { }

[Service(ServiceLifetime.Transient)]
public class MultipleDecoratedServiceDecorator(IMultipleDecoratedService inner) : IMultipleDecoratedService
{
    public IMultipleDecoratedService Inner => inner;
}

public interface IKeyedDecoratedService { }

[Service("decorated", ServiceLifetime.Singleton)]
public class KeyedDecoratedService : IKeyedDecoratedService { }

[Service("other", ServiceLifetime.Singleton)]
public class OtherKeyedDecoratedService : IKeyedDecoratedService { }

[Service("decorated", ServiceLifetime.Singleton)]
public class KeyedDecoratedServiceDecorator(IKeyedDecoratedService inner, SingletonService singleton) : IKeyedDecoratedService
{
    public IKeyedDecoratedService Inner => inner;
    public SingletonService Singleton => singleton;
}

public interface IReadmeNotificationService
{
    void Send(string message);
}

[Service(ServiceLifetime.Scoped)]
public class ReadmeEmailNotificationService : IReadmeNotificationService
{
    public void Send(string message) => Console.WriteLine($"[Email] {message}");
}

[Service(ServiceLifetime.Scoped)]
public class ReadmeLoggingNotificationService(IReadmeNotificationService inner) : IReadmeNotificationService
{
    public IReadmeNotificationService Inner => inner;
    public void Send(string message)
    {
        Console.WriteLine("Sending notification...");
        inner.Send(message);
    }
}

public interface IUnattributedDecorated { }

[Service]
public class UnattributedDecoratedImpl : IUnattributedDecorated { }

// Intentionally no [Service] attribute — decoration should still work.
public class UnattributedDecorator(IUnattributedDecorated inner, SingletonService singleton) : IUnattributedDecorated
{
    public IUnattributedDecorated Inner => inner;
    public SingletonService Singleton => singleton;
}

[Service]
public class IEnumerableDecoratedConsumer(IEnumerable<IMultipleDecoratedService> services)
{
    public IEnumerable<IMultipleDecoratedService> Services => services;
}
