using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Core.Interfaces;
using Relay.Examples.ConditionalRouting;

// This example demonstrates how to use the Relay library to conditionally route payment processing
// based on business rules such as payment method and amount.
// It showcases how to set up a custom context with properties that influence the routing logic.

var services = new ServiceCollection();

// Custom context with business rules
services.AddScoped<IRelayContext>(provider =>
{
    var context = new Relay.Core.Implementations.DefaultRelayContext(provider)
    {
        Properties = { ["PaymentMethod"] = "Stripe", ["Amount"] = 15000m },
    };
    return context;
});

services
    .AddConditionalRelay<IPaymentService>()
    .When(ctx =>
        ctx.Properties.ContainsKey("PaymentMethod")
        && ctx.Properties["PaymentMethod"].Equals("Stripe")
    )
    .RelayTo<StripePaymentService>()
    .When(ctx =>
        ctx.Properties.ContainsKey("PaymentMethod")
        && ctx.Properties["PaymentMethod"].Equals("PayPal")
    )
    .RelayTo<PayPalPaymentService>()
    .When(ctx => ctx.Properties.ContainsKey("Amount") && (decimal)ctx.Properties["Amount"] > 10000)
    .RelayTo<HighValuePaymentService>()
    .Otherwise<DefaultPaymentService>()
    .Build();

var serviceProvider = services.BuildServiceProvider();
var paymentService = serviceProvider.GetRequiredService<IPaymentService>();
var result = await paymentService.ProcessPaymentAsync(15000m, "USD");
Console.WriteLine($"Processed by: {paymentService.ProcessorType}");
Console.WriteLine(result);

namespace Relay.Examples.ConditionalRouting
{
    public class PaymentResult
    {
        public bool Success { get; set; }
        public required string TransactionId { get; set; }

        public override string ToString() => $"Success: {Success}, TransactionId: {TransactionId}";
    }

    public interface IPaymentService
    {
        Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency);
        string ProcessorType { get; }
    }

    public class StripePaymentService : IPaymentService
    {
        public string ProcessorType => "Stripe";

        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            await Task.Delay(200);
            return new PaymentResult { Success = true, TransactionId = $"STRIPE_{Guid.NewGuid()}" };
        }
    }

    public class PayPalPaymentService : IPaymentService
    {
        public string ProcessorType => "PayPal";

        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            await Task.Delay(150);
            return new PaymentResult { Success = true, TransactionId = $"PAYPAL_{Guid.NewGuid()}" };
        }
    }

    public class HighValuePaymentService : IPaymentService
    {
        public string ProcessorType => "HighValue";

        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            await Task.Delay(500);
            return new PaymentResult
            {
                Success = true,
                TransactionId = $"HIGHVAL_{Guid.NewGuid()}",
            };
        }
    }

    public class DefaultPaymentService : IPaymentService
    {
        public string ProcessorType => "Default";

        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            await Task.Delay(100);
            return new PaymentResult
            {
                Success = true,
                TransactionId = $"DEFAULT_{Guid.NewGuid()}",
            };
        }
    }
}
