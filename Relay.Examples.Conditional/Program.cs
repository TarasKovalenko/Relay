using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Examples.Conditional;

// This example demonstrates how to use the Relay library to conditionally relay
// service calls based on the environment. It defines a payment processing service
// that can use different implementations depending on whether the application is
// running in a development, staging, or production environment. The example uses
// a conditional relay pattern to switch between different payment service implementations
// based on the environment context. This is useful for scenarios where you want to
// use mock services in development and testing, while using real services in production.
// The example includes a mock payment service, a Stripe payment service, and a default
// payment service that can be used as a fallback. The Relay library's fluent API
// is used to configure the conditional relays, allowing for a clean and maintainable
// way to manage service dependencies based on the environment.

var services = new ServiceCollection();
services
    .AddConditionalRelay<IPaymentService>()
    .When(ctx => ctx.Environment == "Development")
    .RelayTo<MockPaymentService>()
    .When(ctx => ctx.Environment == "Production")
    .RelayTo<StripePaymentService>()
    .Otherwise<DefaultPaymentService>()
    .Build();

var serviceProvider = services.BuildServiceProvider();
var paymentService = serviceProvider.GetRequiredService<IPaymentService>();
var result = await paymentService.ProcessPaymentAsync(100.00m, "USD");
Console.WriteLine(result);

namespace Relay.Examples.Conditional
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
    }

    public class MockPaymentService : IPaymentService
    {
        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            await Task.Delay(50);
            return new PaymentResult { Success = true, TransactionId = "MOCK_12345" };
        }
    }

    public class StripePaymentService : IPaymentService
    {
        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            await Task.Delay(200);
            return new PaymentResult { Success = true, TransactionId = Guid.NewGuid().ToString() };
        }
    }

    public class DefaultPaymentService : IPaymentService
    {
        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            await Task.Delay(100);
            return new PaymentResult
            {
                Success = true,
                TransactionId = "DEFAULT_" + Guid.NewGuid().ToString()[..8],
            };
        }
    }
}
