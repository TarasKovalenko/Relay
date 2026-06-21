using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Core.Interfaces;
using Relay.Diagnostics;
using Relay.Examples.Observability;

// Production scenario: trace an adapter chain end to end. In a real app you'd wire
// RelayDiagnostics.SourceName into OpenTelemetry; here we attach a plain ActivityListener
// and print each span so you can see the chain's steps.

using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == RelayDiagnostics.SourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStarted = a => Console.WriteLine($"  -> start  {a.OperationName}"),
    ActivityStopped = a =>
        Console.WriteLine(
            $"  <- stop   {a.OperationName} ({a.Duration.TotalMilliseconds:F1} ms) "
                + string.Join(" ", a.Tags.Select(t => $"{t.Key}={t.Value}"))
        ),
};
ActivitySource.AddActivityListener(listener);

var services = new ServiceCollection();
services
    .AddAdapterChain<Receipt>()
    .From<Order>()
    .Then<PricedOrder, PricingAdapter>()
    .Finally<ReceiptAdapter>()
    .Build();

var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();
var chain = scope.ServiceProvider.GetRequiredService<IAdapterChain<Receipt>>();

Console.WriteLine("Executing traced chain:");
var receipt = chain.Execute(new Order("SKU-1", 3));
Console.WriteLine($"Result: {receipt}");

namespace Relay.Examples.Observability
{
    public record Order(string Sku, int Quantity);

    public record PricedOrder(string Sku, decimal Total);

    public record Receipt(string Sku, string Total)
    {
        public override string ToString() => $"{Sku} = {Total}";
    }

    public sealed class PricingAdapter : IAdapter<Order, PricedOrder>
    {
        public PricedOrder Adapt(Order source) => new(source.Sku, source.Quantity * 9.99m);
    }

    public sealed class ReceiptAdapter : IAdapter<PricedOrder, Receipt>
    {
        public Receipt Adapt(PricedOrder source) => new(source.Sku, $"{source.Total:C}");
    }
}
