using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Core.Enums;
using Relay.Core.Interfaces;
using Relay.Examples.MultiFailover;

// This example demonstrates a multi-relay setup with failover strategy.
// It uses multiple storage services, where the primary service is attempted first,
// and if it fails, the secondary and backup services are used in sequence.
// The services implement a simple interface for saving data, simulating potential failures.
// The example shows how to configure the multi-relay with a failover strategy and how to use it in practice.
// The primary service may fail occasionally, while the secondary and backup services are guaranteed to succeed.
// This setup is useful for scenarios where high availability and reliability are critical, such as in cloud storage systems.
// The example includes a loop that attempts to save data multiple times, demonstrating the failover behavior.

var services = new ServiceCollection();
services
    .AddMultiRelay<IStorageService>(config =>
        config
            .AddRelay<PrimaryStorageService>()
            .AddRelay<SecondaryStorageService>()
            .AddRelay<BackupStorageService>()
            .WithStrategy(RelayStrategy.Failover)
    )
    .Build();

var serviceProvider = services.BuildServiceProvider();
var storage = serviceProvider.GetRequiredService<IMultiRelay<IStorageService>>();
for (int i = 0; i < 10; i++)
{
    await storage.RelayToAll(async service => await service.SaveDataAsync("Test data"));
}

namespace Relay.Examples.MultiFailover
{
    public interface IStorageService
    {
        Task<bool> SaveDataAsync(string data);
    }

    public class PrimaryStorageService : IStorageService
    {
        public async Task<bool> SaveDataAsync(string data)
        {
            await Task.Delay(100);
            // Simulate occasional failure
            if (Random.Shared.NextDouble() < 0.3)
                throw new InvalidOperationException("Primary storage unavailable");

            Console.WriteLine($"Primary storage: {data}");
            return true;
        }
    }

    public class SecondaryStorageService : IStorageService
    {
        public async Task<bool> SaveDataAsync(string data)
        {
            await Task.Delay(150);
            Console.WriteLine($"Secondary storage: {data}");
            return true;
        }
    }

    public class BackupStorageService : IStorageService
    {
        public async Task<bool> SaveDataAsync(string data)
        {
            await Task.Delay(200);
            Console.WriteLine($"Backup storage: {data}");
            return true;
        }
    }
}
