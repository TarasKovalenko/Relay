using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Adapters;
using Relay.Core.Enums;
using Relay.Core.Interfaces;
using Relay.Examples.Integration;

// This example demonstrates a complex integration scenario using the Relay library
// with multiple services, conditional routing, multi-channel notifications, and an adapter pattern.
// It showcases how to build a flexible and maintainable architecture using Relay's features.

var services = new ServiceCollection();

// Add core relay services
services.AddRelayServices();

// Basic service registration
services.AddRelay<IOrderService, OrderService>().Build();

// Environment-based payment routing
services
    .AddConditionalRelay<IPaymentService>()
    .When(ctx => ctx.Environment == "Development")
    .RelayTo<MockPaymentService>()
    .When(ctx => ctx.Environment == "Production")
    .RelayTo<StripePaymentService>()
    .Build();

// Multi-channel notifications
services
    .AddMultiRelay<INotificationService>(config =>
        config
            .AddRelay<EmailNotificationService>()
            .AddRelay<SmsNotificationService>()
            .AddRelay<SlackNotificationService>()
            .WithStrategy(RelayStrategy.Broadcast)
    )
    .Build();

// Legacy system adapter
services.AddAdapter<IUserRepository, LegacyUserDatabase>().Using<UserRepositoryAdapter>();

// Payment provider factory
services
    .AddRelayFactory<IPaymentService>(factory =>
        factory
            .RegisterRelay<StripePaymentService>("stripe")
            .RegisterRelay<MockPaymentService>("mock")
            .SetDefaultRelay("stripe")
    )
    .Build();

var serviceProvider = services.BuildServiceProvider();

// Business workflow
var orderService = serviceProvider.GetRequiredService<IOrderService>();
var paymentService = serviceProvider.GetRequiredService<IPaymentService>();
var notifications = serviceProvider.GetRequiredService<IMultiRelay<INotificationService>>();
var userRepository = serviceProvider.GetRequiredService<IUserRepository>();
var paymentFactory = serviceProvider.GetRequiredService<IRelayFactory<IPaymentService>>();

// Create order
var order = await orderService.CreateOrderAsync(new Order { Total = 99.99m });
Console.WriteLine($"Created order {order.Id} for ${order.Total}");

// Get user
var user = await userRepository.GetUserAsync(123);
Console.WriteLine($"User: {user.Name} ({user.Email})");

// Process payment
var payment = await paymentService.ProcessPaymentAsync(order.Total, "USD");
Console.WriteLine($"Payment result: {payment.Success} - {payment.TransactionId}");

if (payment.Success)
{
    // Send notifications to all channels
    await notifications.RelayToAll(async notifier =>
        await notifier.SendNotificationAsync($"Order {order.Id} paid successfully by {user.Name}!")
    );

    // Use specific payment provider for refund
    var mockService = paymentFactory.CreateRelay("mock");
    Console.WriteLine($"Refund service ready: {mockService.GetType().Name}");
}

namespace Relay.Examples.Integration
{
    public class PaymentResult
    {
        public bool Success { get; set; }
        public required string TransactionId { get; set; }

        public override string ToString() => $"Success: {Success}, TransactionId: {TransactionId}";
    }

    public class Order
    {
        public int Id { get; set; }
        public decimal Total { get; set; }
        public string? Status { get; set; }
    }

    public class User
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Email { get; set; }
    }

    public interface IOrderService
    {
        Task<Order> CreateOrderAsync(Order order);
    }

    public interface IPaymentService
    {
        Task<PaymentResult> ProcessPaymentAsync(decimal amount, string currency);
    }

    public interface INotificationService
    {
        Task SendNotificationAsync(string message);
    }

    public interface IUserRepository
    {
        Task<User> GetUserAsync(int id);
    }

    public class OrderService : IOrderService
    {
        public async Task<Order> CreateOrderAsync(Order order)
        {
            await Task.Delay(100);
            order.Id = Random.Shared.Next(1000, 9999);
            order.Status = "Created";
            return order;
        }
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
            return new PaymentResult { Success = true, TransactionId = $"STRIPE_{Guid.NewGuid()}" };
        }
    }

    public class EmailNotificationService : INotificationService
    {
        public async Task SendNotificationAsync(string message)
        {
            await Task.Delay(50);
            Console.WriteLine($"Email: {message}");
        }
    }

    public class SmsNotificationService : INotificationService
    {
        public async Task SendNotificationAsync(string message)
        {
            await Task.Delay(30);
            Console.WriteLine($"SMS: {message}");
        }
    }

    public class SlackNotificationService : INotificationService
    {
        public async Task SendNotificationAsync(string message)
        {
            await Task.Delay(40);
            Console.WriteLine($"Slack: {message}");
        }
    }

    public class LegacyUserDatabase
    {
        public string GetUserData(int userId)
        {
            Thread.Sleep(100);
            return $"LegacyUser_{userId}|John Doe|john@example.com";
        }
    }

    public class UserRepositoryAdapter : IUserRepository
    {
        private readonly LegacyUserDatabase _legacyDb;

        public UserRepositoryAdapter(LegacyUserDatabase legacyDb)
        {
            _legacyDb = legacyDb;
        }

        public async Task<User> GetUserAsync(int id)
        {
            var userData = await Task.Run(() => _legacyDb.GetUserData(id));
            var parts = userData.Split('|');

            return new User
            {
                Id = id,
                Name = parts[1],
                Email = parts[2],
            };
        }
    }
}
