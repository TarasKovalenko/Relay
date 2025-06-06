using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Core.Interfaces;
using Relay.Examples.Factory;

// This example demonstrates how to use the Relay Factory pattern to create a payment processing system
// that can switch between different payment providers (e.g., Stripe, PayPal, Crypto) at runtime.
// The Relay Factory allows for dynamic creation of service instances based on configuration or runtime conditions.
// It uses the Relay pattern to encapsulate the logic for each payment provider, allowing for easy extension and maintenance of the payment processing system.
// The example includes three payment service implementations: StripePaymentService, PayPalPaymentService, and CryptoPaymentService.
// Each service implements the IPaymentService interface, which defines a method for processing payments.
// The Relay Factory is configured to register these services and allows for dynamic creation of service instances based on the provider name.
// The example demonstrates how to create instances of each payment service and process payments using them.
// The output shows the success status and transaction ID for each payment processed.

var services = new ServiceCollection();
services
    .AddRelayFactory<IPaymentService>(factory =>
        factory
            .RegisterRelay<StripePaymentService>("stripe")
            .RegisterRelay<PayPalPaymentService>("paypal")
            .RegisterRelay<CryptoPaymentService>("crypto")
            .SetDefaultRelay("stripe")
    )
    .Build();

var serviceProvider = services.BuildServiceProvider();
var factory = serviceProvider.GetRequiredService<IRelayFactory<IPaymentService>>();

var stripeService = factory.CreateRelay("stripe");
var paypalService = factory.CreateRelay("paypal");
var cryptoService = factory.CreateRelay("crypto");
var defaultService = factory.GetDefaultRelay();

var stripeResult = await stripeService.ProcessPaymentAsync(100m, "USD");
Console.WriteLine(stripeResult);
var paypalResult = await paypalService.ProcessPaymentAsync(100m, "USD");
Console.WriteLine(paypalResult);
var cryptoResult = await cryptoService.ProcessPaymentAsync(100m, "USD");
Console.WriteLine(cryptoResult);
var defaultResult = await defaultService.ProcessPaymentAsync(100m, "USD");
Console.WriteLine(defaultResult);

namespace Relay.Examples.Factory
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
        string ProviderName { get; }
    }

    public class StripePaymentService : IPaymentService
    {
        public string ProviderName => "Stripe";

        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            await Task.Delay(200);
            return new PaymentResult { Success = true, TransactionId = $"STRIPE_{Guid.NewGuid()}" };
        }
    }

    public class PayPalPaymentService : IPaymentService
    {
        public string ProviderName => "PayPal";

        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            await Task.Delay(150);
            return new PaymentResult { Success = true, TransactionId = $"PAYPAL_{Guid.NewGuid()}" };
        }
    }

    public class CryptoPaymentService : IPaymentService
    {
        public string ProviderName => "Crypto";

        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            await Task.Delay(300);
            return new PaymentResult { Success = true, TransactionId = $"CRYPTO_{Guid.NewGuid()}" };
        }
    }
}
