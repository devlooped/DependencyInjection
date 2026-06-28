extern alias Library1;
extern alias Library2;

using System.Diagnostics;
using Merq;
using Microsoft.Extensions.DependencyInjection;

// Initialize services
var collection = new ServiceCollection();

// Library1 contains [Service]-annotated classes, which will be automatically registered here.
collection.AddServices();

// Wrap the echo handler with a stopwatch timer that logs execution time.
collection.Decorate<ICommandHandler<Library1::Library.Echo, string>, EchoHandlerTimer>();

var services = collection.BuildServiceProvider();
var handler = services.GetRequiredService<ICommandHandler<Library1::Library.Echo, string>>();

var message = handler.Execute(new Library1::Library.Echo("Hello"));

Console.WriteLine(message);

[Service]
class EchoHandlerTimer(ICommandHandler<Library1::Library.Echo, string> inner) : ICommandHandler<Library1::Library.Echo, string>
{
    public bool CanExecute(Library1::Library.Echo command) => inner.CanExecute(command);

    public string Execute(Library1::Library.Echo command)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = inner.Execute(command);
        stopwatch.Stop();
        Console.WriteLine($"Echo executed in {stopwatch.Elapsed.TotalMilliseconds:F3} ms");
        return result;
    }
}