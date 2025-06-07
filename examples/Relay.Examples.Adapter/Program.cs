using Microsoft.Extensions.DependencyInjection;
using Relay.Adapters;
using Relay.Examples.Adapter;

// This example demonstrates how to use the Adapter pattern to bridge
// a legacy payment gateway with a modern interface using Relay.Adapters.
// The legacy system has a different method signature, so we create an adapter
// that implements the modern interface and translates calls to the legacy system.

var services = new ServiceCollection();
services
    .AddAdapter<IModernPaymentService, LegacyPaymentGateway>()
    .WithScopedLifetime()
    .WithAdapteeLifetime(ServiceLifetime.Singleton)
    .Using<LegacyPaymentAdapter>();

var serviceProvider = services.BuildServiceProvider();
var paymentService = serviceProvider.GetRequiredService<IModernPaymentService>();
var request = new PaymentRequest
{
    Amount = 100m,
    Currency = "USD",
    CardNumber = "4111111111111111",
};
var response = await paymentService.ProcessAsync(request);
Console.WriteLine(response);

namespace Relay.Examples.Adapter
{
    public interface IModernPaymentService
    {
        Task<PaymentResponse> ProcessAsync(PaymentRequest request);
    }

    public class PaymentRequest
    {
        public decimal Amount { get; set; }
        public required string Currency { get; set; }
        public required string CardNumber { get; set; }
    }

    public class PaymentResponse
    {
        public bool IsSuccessful { get; set; }
        public string? TransactionId { get; set; }
        public string? ErrorMessage { get; set; }

        public override string ToString() =>
            IsSuccessful
                ? $"Success: {IsSuccessful}, TransactionId: {TransactionId}"
                : $"Error: {ErrorMessage}";
    }

    // Legacy system with different interface
    public class LegacyPaymentGateway
    {
        public string ProcessPayment(double amount, string card)
        {
            Thread.Sleep(100);
            return $"LEGACY_{Guid.NewGuid().ToString()[..8]}";
        }
    }

    // Adapter to bridge the gap
    public class LegacyPaymentAdapter(LegacyPaymentGateway legacyGateway) : IModernPaymentService
    {
        public async Task<PaymentResponse> ProcessAsync(PaymentRequest request)
        {
            try
            {
                var transactionId = await Task.Run(
                    () => legacyGateway.ProcessPayment((double)request.Amount, request.CardNumber)
                );

                return new PaymentResponse { IsSuccessful = true, TransactionId = transactionId };
            }
            catch (Exception ex)
            {
                return new PaymentResponse { IsSuccessful = false, ErrorMessage = ex.Message };
            }
        }
    }
}
