using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Spectre.Console.Cli;

namespace Tests.Regressions;

public class Regressions
{
    [Fact]
    public void CovariantRegistrationSatisfiesIntefaceConstraints()
    {
        var collection = new ServiceCollection();
        collection.AddServices(typeof(ICommand));

        var provider = collection.BuildServiceProvider();

        var command = provider.GetRequiredService<MyCommand>();

        Assert.Equal(0, command.Execute(new CommandContext([], Mock.Of<IRemainingArguments>(), "my", null),
            new MySetting { Base = "", Name = "" }));
    }
}

public interface ISetting
{
    string Name { get; set; }
}

public class BaseSetting : CommandSettings
{
    [CommandArgument(0, "<BASE>")]
    public required string Base { get; init; }
}

public class MySetting : BaseSetting, ISetting
{
    [CommandOption("--name")]
    public required string Name { get; set; }
}

public class MyCommand : BaseCommand<MySetting> { }

public abstract class BaseCommand<TSettings> : Command<TSettings> where TSettings : BaseSetting, ISetting
{
    public override int Execute(CommandContext context, TSettings settings)
    {
        Console.WriteLine($"Base: {settings.Base}, Name: {settings.Name}");
        return 0;
    }
}