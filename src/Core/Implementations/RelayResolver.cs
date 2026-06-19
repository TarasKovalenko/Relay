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

        // Flow the context into the ambient accessor so conditional relays and factories
        // resolved below can route based on it, then restore the previous value.
        var accessor = _serviceProvider.GetService<IRelayContextAccessor>();
        if (accessor is null)
        {
            return _serviceProvider.GetRequiredService<TInterface>();
        }

        var previous = accessor.Current;
        accessor.Current = context;
        try
        {
            return _serviceProvider.GetRequiredService<TInterface>();
        }
        finally
        {
            accessor.Current = previous;
        }
    }
}
