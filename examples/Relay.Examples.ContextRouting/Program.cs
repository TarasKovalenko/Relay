using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;
using Relay.Examples.ContextRouting;

// Production scenario: multi-tenant routing. The same IFeatureService resolves to a different
// implementation per request based on the tenant's plan, decided at call time via IRelayContext.

var services = new ServiceCollection();
services.AddRelayServices();
services
    .AddConditionalRelay<IFeatureService>()
    .When(ctx => (string)ctx.Properties["plan"] == "enterprise")
    .RelayTo<EnterpriseFeatures>()
    .When(ctx => (string)ctx.Properties["plan"] == "pro")
    .RelayTo<ProFeatures>()
    .Otherwise<FreeFeatures>()
    .Build();

var provider = services.BuildServiceProvider();

foreach (var plan in new[] { "free", "pro", "enterprise" })
{
    using var scope = provider.CreateScope();
    var resolver = scope.ServiceProvider.GetRequiredService<IRelayResolver>();

    var ctx = new DefaultRelayContext(scope.ServiceProvider);
    ctx.Properties["plan"] = plan;

    var service = resolver.Resolve<IFeatureService>(ctx);
    Console.WriteLine($"plan={plan,-10} -> {service.Describe()}");
}

namespace Relay.Examples.ContextRouting
{
    public interface IFeatureService
    {
        string Describe();
    }

    public sealed class FreeFeatures : IFeatureService
    {
        public string Describe() => "Free: 1 seat, community support";
    }

    public sealed class ProFeatures : IFeatureService
    {
        public string Describe() => "Pro: 10 seats, email support";
    }

    public sealed class EnterpriseFeatures : IFeatureService
    {
        public string Describe() => "Enterprise: unlimited seats, SSO, 24/7 support";
    }
}
