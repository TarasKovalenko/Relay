using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Core.Enums;
using Relay.Core.Interfaces;
using Relay.Examples.Resilience;

// Production scenario: write to a primary storage provider, fall back to a secondary, and
// retry each provider for transient faults before failing over. Mirrors how you'd harden a
// multi-region write path.

var services = new ServiceCollection();
services
    .AddMultiRelay<IStorageProvider>(config =>
        config
            .AddRelay<PrimaryStorage>()
            .AddRelay<SecondaryStorage>()
            .WithStrategy(RelayStrategy.Failover)
            .WithRetry(maxAttempts: 3, delay: TimeSpan.FromMilliseconds(50), backoffFactor: 2.0)
    )
    .Build();

var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var storage = scope.ServiceProvider.GetRequiredService<IMultiRelay<IStorageProvider>>();

// Primary fails its first 2 attempts (transient), succeeds on the 3rd — retry recovers it,
// no failover needed.
var result = await storage.RelayToAllWithResults(s => s.SaveAsync("order-42"));
Console.WriteLine($"Saved via: {string.Join(", ", result)}");

namespace Relay.Examples.Resilience
{
    public interface IStorageProvider
    {
        Task<string> SaveAsync(string payload);
    }

    // Fails the first two attempts to simulate a transient outage, then succeeds.
    public sealed class PrimaryStorage : IStorageProvider
    {
        private int _attempts;

        public async Task<string> SaveAsync(string payload)
        {
            _attempts++;
            await Task.Delay(10);
            if (_attempts < 3)
            {
                Console.WriteLine($"  [primary] attempt {_attempts} failed (transient)");
                throw new TimeoutException("primary temporarily unavailable");
            }
            Console.WriteLine($"  [primary] attempt {_attempts} succeeded");
            return $"primary:{payload}";
        }
    }

    public sealed class SecondaryStorage : IStorageProvider
    {
        public async Task<string> SaveAsync(string payload)
        {
            await Task.Delay(10);
            return $"secondary:{payload}";
        }
    }
}
