using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Core.Enums;
using Relay.Core.Interfaces;
using Relay.Examples.MultiBroadcasting;

// This example demonstrates how to use the Relay library to broadcast notifications
// to multiple services using a multi-relay pattern. Each notification service
// implements the INotificationService interface, and the Relay library is used
// to send a message to all registered services concurrently.
// The services include Email, SMS, and Push notification handlers.
// The example uses a broadcast strategy to ensure that all services receive the notification.
// This is useful in scenarios where you want to notify multiple channels simultaneously,
// such as sending a payment confirmation to email, SMS, and push notifications.
// The example also demonstrates how to configure the Relay library using a fluent API
// to add multiple relays and specify a broadcast strategy.
// The output will show the messages sent by each notification service.

var services = new ServiceCollection();
services
    .AddMultiRelay<INotificationService>(config =>
        config
            .AddRelay<EmailNotificationService>()
            .AddRelay<SmsNotificationService>()
            .AddRelay<PushNotificationService>()
            .WithStrategy(RelayStrategy.Broadcast)
    )
    .Build();

var serviceProvider = services.BuildServiceProvider();
var notifications = serviceProvider.GetRequiredService<IMultiRelay<INotificationService>>();
await notifications.RelayToAll(async service =>
    await service.SendNotificationAsync("Payment processed successfully!")
);

namespace Relay.Examples.MultiBroadcasting
{
    public interface INotificationService
    {
        Task SendNotificationAsync(string message);
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

    public class PushNotificationService : INotificationService
    {
        public async Task SendNotificationAsync(string message)
        {
            await Task.Delay(20);
            Console.WriteLine($"Push: {message}");
        }
    }
}
