using Microsoft.Extensions.DependencyInjection;
using Relay.Adapters;
using Relay.Examples.AdapterFactory;

// This example demonstrates how to use the Adapter pattern to adapt a third-party email service
// to a custom notification interface using Relay's adapter registration extensions.

var services = new ServiceCollection();
services
    .AddAdapter<INotificationService, ThirdPartyEmailService>()
    .Using(emailService => new EmailNotificationAdapter(emailService));

var serviceProvider = services.BuildServiceProvider();
var notificationService = serviceProvider.GetRequiredService<INotificationService>();
await notificationService.SendAsync("Hello World!", "user@example.com");

namespace Relay.Examples.AdapterFactory
{
    public interface INotificationService
    {
        Task SendAsync(string message, string recipient);
    }

    public class ThirdPartyEmailService
    {
        public void SendEmail(string to, string subject, string body)
        {
            Console.WriteLine($"Third-party email to {to}: {subject} - {body}");
        }
    }

    public class EmailNotificationAdapter(ThirdPartyEmailService emailService)
        : INotificationService
    {
        public async Task SendAsync(string message, string recipient)
        {
            await Task.Run(() => emailService.SendEmail(recipient, "Notification", message));
        }
    }
}
