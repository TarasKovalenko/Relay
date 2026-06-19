using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Core.Interfaces;
using Relay.Examples.AsyncChain;

// Demonstrates an asynchronous adapter chain: OrderId -> OrderDto -> Invoice.
// Each step performs (simulated) async I/O via IAsyncAdapter, so no thread is blocked.

var services = new ServiceCollection();
services
    .AddAsyncAdapterChain<Invoice>()
    .From<OrderId>()
    .Then<OrderDto, FetchOrderAdapter>()
    .Finally<InvoiceAdapter>()
    .Build();

var provider = services.BuildServiceProvider();
using var scope = provider.CreateScope();

var chain = scope.ServiceProvider.GetRequiredService<IAsyncAdapterChain<Invoice>>();
var invoice = await chain.ExecuteAsync(new OrderId(42));
Console.WriteLine(invoice);

namespace Relay.Examples.AsyncChain
{
    public record OrderId(int Value);

    public record OrderDto(int Id, decimal Amount);

    public record Invoice(int OrderId, string Total)
    {
        public override string ToString() => $"Invoice for order {OrderId}: {Total}";
    }

    public class FetchOrderAdapter : IAsyncAdapter<OrderId, OrderDto>
    {
        public async Task<OrderDto> AdaptAsync(OrderId source, CancellationToken cancellationToken = default)
        {
            await Task.Delay(100, cancellationToken); // simulate an API/database call
            return new OrderDto(source.Value, 199.99m);
        }
    }

    public class InvoiceAdapter : IAsyncAdapter<OrderDto, Invoice>
    {
        public async Task<Invoice> AdaptAsync(OrderDto source, CancellationToken cancellationToken = default)
        {
            await Task.Delay(50, cancellationToken); // simulate rendering/persisting
            return new Invoice(source.Id, $"{source.Amount:C}");
        }
    }
}
