using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Decorators;
using Relay.Examples.DecorateWith;

// This example demonstrates how to use the Relay library to decorate a service with multiple decorators.
// It shows how to apply decorators in a flexible way, allowing for single or multiple decorators to be applied to a service.
// The example includes a payment service that can be decorated with logging, caching, and retry functionality.
// The decorators can be applied in a fluent manner, making it easy to compose complex behaviors.
// The final service will be a combination of all decorators applied in the order they were specified.

var services = new ServiceCollection();

// Single decorator
services
    .AddRelay<IPaymentService, StripePaymentService>()
    .WithScopedLifetime()
    .DecorateWith<LoggingPaymentDecorator>()
    .Build();

// Multiple decorators (nested)
services.Clear();
services
    .AddRelay<IPaymentService, StripePaymentService>()
    .WithScopedLifetime()
    .DecorateWith<CachingPaymentDecorator>()
    .DecorateWith<LoggingPaymentDecorator>()
    .Build();

// Using extension methods directly
services.Clear();
services.AddScoped<IPaymentService, StripePaymentService>();
services.Decorate<IPaymentService>(typeof(LoggingPaymentDecorator));
services.Decorate<IPaymentService>((service, _) => new CachingPaymentDecorator(service));
services.Decorate<IPaymentService>((service, _) => new RetryPaymentDecorator(service, 2));

var serviceProvider = services.BuildServiceProvider();
var paymentService = serviceProvider.GetRequiredService<IPaymentService>();

// The service is now: RetryPaymentDecorator -> CachingPaymentDecorator -> LoggingPaymentDecorator -> StripePaymentService
var result = await paymentService.ProcessPaymentAsync(100m, "USD");
Console.WriteLine($"Final result: {result.Success} - {result.TransactionId}");

namespace Relay.Examples.DecorateWith
{
    public interface IPaymentService
    {
        Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency);
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string? TransactionId { get; set; }
        public decimal Amount { get; set; }
    }

    // Base payment service
    public class StripePaymentService : IPaymentService
    {
        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            await Task.Delay(200);
            return new PaymentResult
            {
                Success = true,
                TransactionId = $"STRIPE_{Guid.NewGuid()}",
                Amount = amount,
            };
        }
    }

    // Logging decorator
    public class LoggingPaymentDecorator : IPaymentService
    {
        private readonly IPaymentService _inner;

        public LoggingPaymentDecorator(IPaymentService inner)
        {
            _inner = inner;
        }

        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            Console.WriteLine($"[LOG] Processing payment: ${amount} {currency}");
            var result = await _inner.ProcessPaymentAsync(amount, currency);
            Console.WriteLine($"[LOG] Payment result: {result.Success} - {result.TransactionId}");
            return result;
        }
    }

    // Caching decorator
    public class CachingPaymentDecorator : IPaymentService
    {
        private readonly IPaymentService _inner;
        private static readonly Dictionary<string, PaymentResult> _cache = new();

        public CachingPaymentDecorator(IPaymentService inner)
        {
            _inner = inner;
        }

        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            var key = $"{amount}_{currency}";

            if (_cache.TryGetValue(key, out var cached))
            {
                Console.WriteLine($"[CACHE] Found cached result for {key}");
                return cached;
            }

            var result = await _inner.ProcessPaymentAsync(amount, currency);
            _cache[key] = result;
            Console.WriteLine($"[CACHE] Cached result for {key}");
            return result;
        }
    }

    // Retry decorator
    public class RetryPaymentDecorator : IPaymentService
    {
        private readonly IPaymentService _inner;
        private readonly int _maxRetries;

        public RetryPaymentDecorator(IPaymentService inner, int maxRetries = 3)
        {
            _inner = inner;
            _maxRetries = maxRetries;
        }

        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            for (int attempt = 1; attempt <= _maxRetries; attempt++)
            {
                try
                {
                    Console.WriteLine($"[RETRY] Attempt {attempt}/{_maxRetries}");
                    return await _inner.ProcessPaymentAsync(amount, currency);
                }
                catch (Exception ex) when (attempt < _maxRetries)
                {
                    Console.WriteLine($"[RETRY] Attempt {attempt} failed: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt))); // Exponential backoff
                }
            }

            // Final attempt without catching
            return await _inner.ProcessPaymentAsync(amount, currency);
        }
    }
}
