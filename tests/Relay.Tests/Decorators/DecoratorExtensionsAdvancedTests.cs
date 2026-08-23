using Microsoft.Extensions.DependencyInjection;
using Relay.Decorators;
using Shouldly;

namespace Relay.Tests.Decorators;

public class DecoratorExtensionsAdvancedTests
{
    [Fact]
    public void Decorate_Generic_ShouldWrapTheRegisteredService()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGreeter, Greeter>();

        services.Decorate<IGreeter, LoudGreeter>();

        var greeter = services.BuildServiceProvider().GetRequiredService<IGreeter>();
        greeter.ShouldBeOfType<LoudGreeter>();
        greeter.Greet().ShouldBe("HELLO!");
    }

    [Fact]
    public void Decorate_ShouldWrapEveryRegistrationOfTheService()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGreeter, Greeter>();
        services.AddScoped<IGreeter, FormalGreeter>();

        services.Decorate<IGreeter, LoudGreeter>();

        var greeters = services.BuildServiceProvider().GetServices<IGreeter>().ToList();
        greeters.Count.ShouldBe(2);
        greeters.ShouldAllBe(g => g is LoudGreeter);
        greeters.Select(g => g.Greet()).ShouldBe(["HELLO!", "GOOD DAY!"]);
    }

    [Fact]
    public void Decorate_ShouldStackInRegistrationOrder()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGreeter, Greeter>();

        services.Decorate<IGreeter, LoudGreeter>();
        services.Decorate<IGreeter, ExcitedGreeter>();

        var greeter = services.BuildServiceProvider().GetRequiredService<IGreeter>();
        greeter.Greet().ShouldBe("HELLO!!!");
    }

    [Fact]
    public void Decorate_ShouldPreserveTheOriginalLifetime()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGreeter, Greeter>();

        services.Decorate<IGreeter, LoudGreeter>();

        services
            .Single(d => d.ServiceType == typeof(IGreeter))
            .Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Decorate_ShouldWrapAnInstanceRegistration()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGreeter>(new Greeter());

        services.Decorate<IGreeter, LoudGreeter>();

        services.BuildServiceProvider().GetRequiredService<IGreeter>().Greet().ShouldBe("HELLO!");
    }

    [Fact]
    public void Decorate_ShouldIgnoreKeyedRegistrations()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IGreeter, Greeter>("primary");

        // Keyed descriptors used to be picked up here and threw when their ImplementationType
        // was read; they must simply not match.
        services.TryDecorate<IGreeter, LoudGreeter>().ShouldBeFalse();
    }

    [Fact]
    public void DecorateKeyed_ShouldWrapTheKeyedRegistration()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IGreeter, Greeter>("primary");
        services.AddKeyedScoped<IGreeter, FormalGreeter>("secondary");

        services.DecorateKeyed<IGreeter, LoudGreeter>("primary");

        var provider = services.BuildServiceProvider();
        provider.GetRequiredKeyedService<IGreeter>("primary").Greet().ShouldBe("HELLO!");
        provider.GetRequiredKeyedService<IGreeter>("secondary").Greet().ShouldBe("Good day!");
    }

    [Fact]
    public void DecorateKeyed_ShouldThrowWhenTheKeyIsNotRegistered()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IGreeter, Greeter>("primary");

        Should.Throw<InvalidOperationException>(() =>
            services.DecorateKeyed<IGreeter, LoudGreeter>("missing")
        );
    }

    [Fact]
    public void TryDecorate_ShouldReturnFalseWhenTheServiceIsNotRegistered()
    {
        var services = new ServiceCollection();

        services.TryDecorate<IGreeter, LoudGreeter>().ShouldBeFalse();
        services.ShouldBeEmpty();
    }

    [Fact]
    public void TryDecorate_WithFactory_ShouldWrapTheRegisteredService()
    {
        var services = new ServiceCollection();
        services.AddScoped<IGreeter, Greeter>();

        services
            .TryDecorate<IGreeter>((inner, _) => new LoudGreeter(inner))
            .ShouldBeTrue();

        services.BuildServiceProvider().GetRequiredService<IGreeter>().Greet().ShouldBe("HELLO!");
    }

    [Fact]
    public void Decorate_ShouldThrowWhenTheServiceIsNotRegistered()
    {
        var services = new ServiceCollection();

        Should.Throw<InvalidOperationException>(() => services.Decorate<IGreeter, LoudGreeter>());
    }

    [Fact]
    public void Decorate_ShouldThrowOnNullArguments()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
            ((IServiceCollection)null!).Decorate<IGreeter>(typeof(LoudGreeter))
        );
        Should.Throw<ArgumentNullException>(() => services.Decorate<IGreeter>((Type)null!));
        Should.Throw<ArgumentNullException>(() =>
            services.Decorate<IGreeter>((Func<IGreeter, IServiceProvider, IGreeter>)null!)
        );
    }

    private interface IGreeter
    {
        string Greet();
    }

    private sealed class Greeter : IGreeter
    {
        public string Greet() => "Hello";
    }

    private sealed class FormalGreeter : IGreeter
    {
        public string Greet() => "Good day!";
    }

    private sealed class LoudGreeter(IGreeter inner) : IGreeter
    {
        public string Greet() => inner.Greet().ToUpperInvariant().TrimEnd('!') + "!";
    }

    private sealed class ExcitedGreeter(IGreeter inner) : IGreeter
    {
        public string Greet() => inner.Greet() + "!!";
    }
}
