using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Examples.Basic;

// This example demonstrates how to use the Relay library to create a simple payment service
// that processes payments using a specific implementation (StripePaymentService).
// The service is registered with a scoped lifetime, meaning it will be created once per request scope.
// The example then retrieves the service from the service provider and calls the ProcessPaymentAsync method.

var services = new ServiceCollection();
services.AddRelay<IPaymentService, StripePaymentService>().WithScopedLifetime().Build();

var serviceProvider = services.BuildServiceProvider();
var paymentService = serviceProvider.GetRequiredService<IPaymentService>();
var result = await paymentService.ProcessPaymentAsync(100.00m, "USD");
Console.WriteLine(result);

namespace Relay.Examples.Basic
{
    public interface IPaymentService
    {
        Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency);
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public required string TransactionId { get; set; }

        public override string ToString() => $"Success: {Success}, TransactionId: {TransactionId}";
    }

    public class StripePaymentService : IPaymentService
    {
        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            await Task.Delay(200);
            return new PaymentResult { Success = true, TransactionId = Guid.NewGuid().ToString() };
        }
    }
}
