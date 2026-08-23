using Microsoft.Extensions.DependencyInjection;
using Relay;
using Relay.Core.Interfaces;
using Relay.Examples.LifetimeSafety;
using Relay.Extensions;
using Relay.Validation;

// The three lifetime mistakes that keep showing up in real applications, and what Relay does
// about them: catching a captive dependency before it ships, deferring an expensive service,
// and using a scoped service from a singleton.

// 1. A broken graph: the singleton would capture the scoped unit of work forever.
var broken = new ServiceCollection();
broken.AddScoped<IUnitOfWork, UnitOfWork>();
broken.AddSingleton<ReportScheduler>();

var result = broken.ValidateRelayRegistrations();
Console.WriteLine("=== Validation ===");
Console.WriteLine(result);

// 2. The fixed graph: the scheduler asks for a scope instead of capturing the service.
var services = new ServiceCollection();
services.AddRelayServices();
services.AddLazyResolution();
services.AddScoped<IUnitOfWork, UnitOfWork>();
services.AddSingleton<SafeReportScheduler>();
services.AddSingleton<ExpensiveReportRenderer>();

services.ValidateRelayRegistrationsOrThrow();
Console.WriteLine("Fixed graph validates cleanly.");

var provider = services.BuildServiceProvider();

// 3. A singleton doing scoped work, one scope per run.
var scheduler = provider.GetRequiredService<SafeReportScheduler>();
await scheduler.RunAsync();
await scheduler.RunAsync();

// 4. Lazy resolution: the renderer is only constructed when it is first used.
Console.WriteLine($"Renderer constructed: {ExpensiveReportRenderer.Constructed}");
var lazyRenderer = provider.GetRequiredService<Lazy<ExpensiveReportRenderer>>();
Console.WriteLine($"After resolving Lazy<T>: {ExpensiveReportRenderer.Constructed}");
Console.WriteLine(lazyRenderer.Value.Render());
Console.WriteLine($"After reading .Value: {ExpensiveReportRenderer.Constructed}");

namespace Relay.Examples.LifetimeSafety
{
    public interface IUnitOfWork
    {
        Guid Id { get; }
    }

    public sealed class UnitOfWork : IUnitOfWork
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    /// <summary>Captures a scoped unit of work in a singleton: reported by validation.</summary>
    public sealed class ReportScheduler(IUnitOfWork unitOfWork)
    {
        public IUnitOfWork UnitOfWork { get; } = unitOfWork;
    }

    /// <summary>Asks for a scope per run instead of holding on to the scoped service.</summary>
    public sealed class SafeReportScheduler(IRelayScopeRunner scopeRunner)
    {
        public Task RunAsync() =>
            scopeRunner.RunAsync<IUnitOfWork>(
                (unitOfWork, _) =>
                {
                    Console.WriteLine($"Report run with unit of work {unitOfWork.Id}");
                    return Task.CompletedTask;
                }
            );
    }

    public sealed class ExpensiveReportRenderer
    {
        public static bool Constructed { get; private set; }

        public ExpensiveReportRenderer() => Constructed = true;

        public string Render() => "Rendered report";
    }
}
