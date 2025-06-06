using Microsoft.Extensions.DependencyInjection;
using Relay.Decorators;
using Relay.Examples.AdvancedDecorator;

var services = new ServiceCollection();

services.AddScoped<IPaymentService, StripePaymentService>();

// Apply decorators in order (inner to outer)
// Validates first
services.Decorate<IPaymentService>(typeof(ValidationPaymentDecorator));

// Logs validated requests
services.Decorate<IPaymentService>(typeof(LoggingPaymentDecorator));

// Measures performance
services.Decorate<IPaymentService>(typeof(MetricsPaymentDecorator));

// Prevents cascading failures
services.Decorate<IPaymentService>(typeof(CircuitBreakerPaymentDecorator));

// Retries on failure
services.Decorate<IPaymentService>(typeof(RetryPaymentDecorator));

// Caches successful results
services.Decorate<IPaymentService>(typeof(CachingPaymentDecorator));

var serviceProvider2 = services.BuildServiceProvider();
var decoratedService = serviceProvider2.GetRequiredService<IPaymentService>();

// Test the fully decorated service
try
{
    await decoratedService.ProcessPaymentAsync(50m, "USD");
    // Should hit cache
    await decoratedService.ProcessPaymentAsync(50m, "USD");
    await decoratedService.ProcessPaymentAsync(75m, "EUR");
}
catch (Exception ex)
{
    Console.WriteLine($"Payment failed: {ex.Message}");
}

namespace Relay.Examples.AdvancedDecorator
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

    // Validation decorator
    public class ValidationPaymentDecorator : IPaymentService
    {
        private readonly IPaymentService _inner;

        public ValidationPaymentDecorator(IPaymentService inner)
        {
            _inner = inner;
        }

        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            if (amount <= 0)
                throw new ArgumentException("Amount must be positive");

            if (string.IsNullOrEmpty(currency))
                throw new ArgumentException("Currency is required");

            if (currency.Length != 3)
                throw new ArgumentException("Currency must be 3 characters");

            Console.WriteLine($"[VALIDATION] Payment validated: ${amount} {currency}");
            return await _inner.ProcessPaymentAsync(amount, currency);
        }
    }

    // Metrics decorator
    public class MetricsPaymentDecorator : IPaymentService
    {
        private readonly IPaymentService _inner;
        private static int _totalPayments = 0;
        private static decimal _totalAmount = 0;

        public MetricsPaymentDecorator(IPaymentService inner)
        {
            _inner = inner;
        }

        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var result = await _inner.ProcessPaymentAsync(amount, currency);

                if (result.Success)
                {
                    Interlocked.Increment(ref _totalPayments);
                    _totalAmount += amount;
                }

                stopwatch.Stop();
                Console.WriteLine(
                    $"[METRICS] Payment processed in {stopwatch.ElapsedMilliseconds}ms. Total payments: {_totalPayments}, Total amount: ${_totalAmount}"
                );

                return result;
            }
            catch
            {
                stopwatch.Stop();
                Console.WriteLine(
                    $"[METRICS] Payment failed after {stopwatch.ElapsedMilliseconds}ms"
                );
                throw;
            }
        }
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

    // Circuit breaker decorator
    public class CircuitBreakerPaymentDecorator : IPaymentService
    {
        private readonly IPaymentService _inner;
        private int _failureCount = 0;
        private DateTime? _lastFailureTime = null;
        private readonly int _threshold = 3;
        private readonly TimeSpan _timeout = TimeSpan.FromMinutes(1);

        public CircuitBreakerPaymentDecorator(IPaymentService inner)
        {
            _inner = inner;
        }

        public async Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency)
        {
            if (
                _failureCount >= _threshold
                && _lastFailureTime.HasValue
                && DateTime.UtcNow - _lastFailureTime.Value < _timeout
            )
            {
                Console.WriteLine("[CIRCUIT BREAKER] Circuit is open, payment blocked");
                return new PaymentResult { Success = false, TransactionId = "CIRCUIT_OPEN" };
            }

            try
            {
                var result = await _inner.ProcessPaymentAsync(amount, currency);

                if (result.Success)
                {
                    _failureCount = 0; // Reset on success
                    Console.WriteLine("[CIRCUIT BREAKER] Payment successful, circuit reset");
                }

                return result;
            }
            catch (Exception)
            {
                _failureCount++;
                _lastFailureTime = DateTime.UtcNow;
                Console.WriteLine($"[CIRCUIT BREAKER] Failure #{_failureCount}, circuit may open");
                throw;
            }
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
