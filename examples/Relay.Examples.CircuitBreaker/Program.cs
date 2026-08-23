using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Core.Enums;
using Relay.Core.Implementations;
using Relay.Core.Interfaces;
using Relay.Diagnostics;
using Relay.Examples.CircuitBreaker;

// A primary payment provider goes down. Retry and failover alone would keep paying for its
// timeouts on every request; the circuit breaker stops calling it after two consecutive failures
// and gives it one trial request after the break. Everything is reported through the Relay meter,
// so the same numbers would land in Prometheus/OTLP in a real application.

using var listener = new MeterListener();
listener.InstrumentPublished = (instrument, l) =>
{
    if (instrument.Meter.Name == RelayDiagnostics.MeterName)
    {
        l.EnableMeasurementEvents(instrument);
    }
};
listener.SetMeasurementEventCallback<long>(
    (instrument, value, tags, _) =>
    {
        if (instrument.Name.StartsWith("relay.circuit_breaker", StringComparison.Ordinal))
        {
            var rendered = string.Join(
                ", ",
                tags.ToArray().Select(t => $"{t.Key}={t.Value}")
            );
            Console.WriteLine($"  [metric] {instrument.Name} {value:+#;-#;0} {{{rendered}}}");
        }
    }
);
listener.Start();

var services = new ServiceCollection();
services
    .AddMultiRelay<IPaymentProvider>(config =>
        config
            .AddRelay<PrimaryProvider>()
            .AddRelay<BackupProvider>()
            .WithStrategy(RelayStrategy.Failover)
            .WithCircuitBreaker(failureThreshold: 2, breakDuration: TimeSpan.FromMilliseconds(300))
    )
    .Build();

var provider = services.BuildServiceProvider();
var relay = (MultiRelay<IPaymentProvider>)
    provider.GetRequiredService<IMultiRelay<IPaymentProvider>>();

Console.WriteLine("=== Primary is down ===");
for (var i = 1; i <= 4; i++)
{
    var result = await relay.RelayToAllWithResults(p => p.ChargeAsync(19.99m));
    Console.WriteLine($"Request {i} -> {result.Single()} (primary calls so far: {PrimaryProvider.Calls})");
    PrintCircuits(relay);
}

Console.WriteLine();
Console.WriteLine("=== Primary recovers, break elapses ===");
PrimaryProvider.Healthy = true;
await Task.Delay(350);

var recovered = await relay.RelayToAllWithResults(p => p.ChargeAsync(19.99m));
Console.WriteLine($"Request 5 -> {recovered.Single()}");
PrintCircuits(relay);

static void PrintCircuits(MultiRelay<IPaymentProvider> relay)
{
    foreach (var circuit in relay.GetCircuitSnapshots())
    {
        Console.WriteLine(
            $"  {circuit.RelayType.Name}: {circuit.State} (consecutive failures: {circuit.ConsecutiveFailures})"
        );
    }
}

namespace Relay.Examples.CircuitBreaker
{
    public interface IPaymentProvider
    {
        Task<string> ChargeAsync(decimal amount);
    }

    public sealed class PrimaryProvider : IPaymentProvider
    {
        internal static int Calls;
        internal static bool Healthy;

        public Task<string> ChargeAsync(decimal amount)
        {
            Calls++;
            return Healthy
                ? Task.FromResult($"charged {amount} via primary")
                : Task.FromException<string>(new TimeoutException("primary is unreachable"));
        }
    }

    public sealed class BackupProvider : IPaymentProvider
    {
        public Task<string> ChargeAsync(decimal amount) =>
            Task.FromResult($"charged {amount} via backup");
    }
}
