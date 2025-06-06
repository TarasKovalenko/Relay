using Microsoft.Extensions.DependencyInjection;
using Relay.Core.Interfaces;

namespace Relay.Core.Implementations;

public sealed class RelayResolver(IServiceProvider serviceProvider) : IRelayResolver
{
    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public TInterface Resolve<TInterface>(IRelayContext? context = null)
        where TInterface : class
    {
        context ??= new DefaultRelayContext(_serviceProvider);
        return _serviceProvider.GetRequiredService<TInterface>();
    }
}
