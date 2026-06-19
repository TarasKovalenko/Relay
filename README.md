# 🔗 Relay - Adaptive Dependency Injection

[![Made in Ukraine](https://img.shields.io/badge/made_in-ukraine-ffd700.svg?labelColor=0057b7)](https://taraskovalenko.github.io/)
[![Relay](https://img.shields.io/nuget/v/Relay?label=Relay)](https://www.nuget.org/packages/Relay)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Relay your dependency injection to the next level! A powerful, fluent library that extends Microsoft.Extensions.DependencyInjection with adaptive patterns for conditional routing, multi-relays, adapter chains, and dynamic service resolution.

![Relay](https://repository-images.githubusercontent.com/995483153/4675d8d7-ec1b-43ce-8dd5-c6d736a85c70)

## Terms of use

By using this project or its source code, for any purpose and in any shape or form, you grant your **implicit agreement** to all of the following statements:

- You unequivocally condemn Russia and its military aggression against Ukraine
- You recognize that Russia is an occupant that unlawfully invaded a sovereign state
- You agree that [Russia is a terrorist state](https://www.europarl.europa.eu/doceo/document/RC-9-2022-0482_EN.html)
- You fully support Ukraine's territorial integrity, including its claims over [temporarily occupied territories](https://en.wikipedia.org/wiki/Russian-occupied_territories_of_Ukraine)
- You reject false narratives perpetuated by Russian state propaganda

To learn more about the war and how you can help, [click here](https://war.ukraine.ua/). Glory to Ukraine! 🇺🇦

## 🎯 **Why Relay?**

Transform the adapter pattern from a simple design pattern into a **powerful architectural tool**:

- **🔄 Conditional Routing**: Route to different implementations based on environment, context, or runtime conditions
- **📡 Multi-Relay Broadcasting**: Execute operations across multiple implementations with various strategies
- **🔗 True Adapter Pattern**: Seamlessly integrate legacy systems and incompatible interfaces
- **⛓️ Adapter Chains**: Build complex transformation pipelines (A→B→C→X)
- **🏭 Relay Factories**: Key-based service creation with native keyed-DI support
- **⏱️ Async Pipelines**: `IAsyncAdapter` chains for non-blocking I/O transformations
- **🔭 Observability**: Built-in `ActivitySource` tracing for chains and multi-relays
- **⚡ Performance Optimized**: Cached reflection and lock-free round-robin resolution

## 🚀 **Quick Start**

### Installation
```bash
dotnet add package Relay
```

> **Requirements:** .NET 10.0 or later.

### Basic Usage
```csharp
using Relay;

// Configure services
services.AddRelayServices();

// Basic relay
services.AddRelay<IPaymentService, StripePaymentRelay>()
    .WithScopedLifetime()
    .Build();

// Conditional relay
services.AddConditionalRelay<IPaymentService>()
    .When(ctx => ctx.Environment == "Development").RelayTo<MockPaymentService>()
    .When(ctx => ctx.Environment == "Production").RelayTo<StripePaymentService>()
    .Build();

// Multi-relay broadcasting
services.AddMultiRelay<INotificationService>(config => config
    .AddRelay<EmailNotificationService>()
    .AddRelay<SmsNotificationService>()
    .WithStrategy(RelayStrategy.Broadcast)
).Build();
```

## 📋 **Complete Feature Set**

### **1. Basic Relay Registration**
```csharp
services.AddRelay<IPaymentService, StripePaymentRelay>()
    .WithScopedLifetime()
    .DecorateWith<LoggingDecorator>()
    .Build();
```

### **2. Conditional Routing**
```csharp
services.AddConditionalRelay<IPaymentService>()
    .WithScopedLifetime()
    .When(ctx => ctx.Environment == "Development")
        .RelayTo<MockPaymentService>()
    .When(ctx => ctx.Properties["PaymentMethod"].Equals("Stripe"))
        .RelayTo<StripePaymentService>()
    .When(ctx => ctx.Properties["PaymentMethod"].Equals("PayPal"))
        .RelayTo<PayPalPaymentService>()
    .Otherwise<DefaultPaymentService>()
    .Build();
```

### **3. Multi-Relay Broadcasting**
```csharp
// Broadcast to all services
services.AddMultiRelay<INotificationService>(config => config
    .AddRelay<EmailNotificationService>(ServiceLifetime.Singleton)
    .AddRelay<SmsNotificationService>(ServiceLifetime.Scoped)
    .AddRelay<PushNotificationService>(ServiceLifetime.Transient)
    .WithStrategy(RelayStrategy.Broadcast)
).Build();

// Failover strategy
services.AddMultiRelay<IStorageService>(config => config
    .AddRelay<PrimaryStorageService>()
    .AddRelay<SecondaryStorageService>()
    .AddRelay<BackupStorageService>()
    .WithStrategy(RelayStrategy.Failover)
).Build();
```

### **4. True Adapter Pattern** 
```csharp
// Wrap an incompatible service in an adapter
services.AddAdapter<ITarget, Adaptee>()
    .WithScopedLifetime()
    .Using<Adapter>();

// Legacy system integration
services.AddAdapter<IModernPaymentService, LegacyPaymentGateway>()
    .WithScopedLifetime()
    .WithAdapteeLifetime(ServiceLifetime.Singleton)
    .Using<LegacyPaymentAdapter>();

// Factory-based adapters
services.AddAdapter<INotificationService, ThirdPartyEmailService>()
    .Using(emailService => new EmailNotificationAdapter(emailService));
```

### **5. Adapter Chains (A→B→C→X)**
```csharp
// Complex transformation pipeline
services.AddAdapterChain<IResult>()
    .From<RawData>()                           // A (source)
    .Then<ValidatedData, ValidationAdapter>()  // A → B
    .Then<EnrichedData, EnrichmentAdapter>()   // B → C  
    .Finally<ProcessedResultAdapter>()         // C → X (final)
    .Build();

// Strongly-typed chain
services.AddTypedAdapterChain<XmlData, DomainModel>()
    .Then<JsonData, XmlToJsonAdapter>()
    .Then<DataDto, JsonToDtoAdapter>()  
    .Then<DomainModel, DtoToDomainAdapter>()
    .Build();

// Named chains — several pipelines producing the same result, picked by name at runtime.
// The source instance is resolved from the container.
services.AddSingleton(new RawData(...));
services.AddAdapterChainFactory<ISettings>()
    .AddChain("full")
        .From<RawData>().Then<Validated, ValidateAdapter>().Finally<SettingsAdapter>()
    .AddChain("fast")
        .From<RawData>().Finally<DirectSettingsAdapter>()
    .AddChain("mock", _ => new MockSettings())   // or a plain producer delegate
    .Build();

var factory = serviceProvider.GetRequiredService<IAdapterChainFactory<ISettings>>();
var settings = factory.CreateFromChain("full");
var names    = factory.GetAvailableChains();   // ["full", "fast", "mock"]
```

### **6. Relay Factory**
```csharp
services.AddRelayFactory<IPaymentService>(factory => factory
    .RegisterRelay("stripe", provider => new StripeRelay())
    .RegisterRelay("paypal", provider => new PayPalRelay())
    .RegisterRelay("crypto", provider => new CryptoRelay())
    .SetDefaultRelay("stripe")
).Build();

// Usage
var factory = serviceProvider.GetService<IRelayFactory<IPaymentService>>();
var paymentService = factory.CreateRelay("stripe");
```

### **7. Auto-Discovery**
```csharp
services.AddRelay(config => config
    .FromAssemblyOf<IPaymentService>()
    .WithDefaultLifetime(ServiceLifetime.Scoped)
    .RegisterRelays()
);

// Advanced discovery
services.AddAdaptersFromAssembly<IPaymentService>(ServiceLifetime.Scoped);
```

### **8. Comprehensive Lifetime Management**
```csharp
// Different lifetimes for different components
services.AddMultiRelay<INotificationService>(config => config
    .WithDefaultLifetime(ServiceLifetime.Scoped)
    .AddRelay<EmailService>(ServiceLifetime.Singleton)    // Expensive to create
    .AddRelay<SmsService>(ServiceLifetime.Scoped)         // Request-specific
    .AddRelay<PushService>(ServiceLifetime.Transient)     // Independent operations
    .WithStrategy(RelayStrategy.Broadcast)
).Build();
```

### **9. Async Adapter Chains**
For transformation pipelines that perform I/O (HTTP, database, file access), use async adapters so steps never block a thread.
```csharp
public class FetchAdapter : IAsyncAdapter<OrderId, OrderDto>
{
    public async Task<OrderDto> AdaptAsync(OrderId id, CancellationToken ct = default)
        => await _api.GetOrderAsync(id, ct);
}

services.AddAsyncAdapterChain<Invoice>()
    .From<OrderId>()
    .Then<OrderDto, FetchAdapter>()      // OrderId → OrderDto (async I/O)
    .Then<EnrichedOrder, EnrichAdapter>()
    .Finally<InvoiceAdapter>()
    .Build();

// Usage
var chain = serviceProvider.GetRequiredService<IAsyncAdapterChain<Invoice>>();
var invoice = await chain.ExecuteAsync(new OrderId(42), cancellationToken);
```

### **10. Context-Aware Resolution**
`IRelayResolver` now flows an explicit `IRelayContext` into conditional relays and factories, so routing decisions can be made per call.
```csharp
services.AddRelayServices();
services.AddConditionalRelay<IPaymentService>()
    .When(ctx => (string)ctx.Properties["tier"] == "premium").RelayTo<PremiumPayment>()
    .Otherwise<StandardPayment>()
    .Build();

var resolver = scope.ServiceProvider.GetRequiredService<IRelayResolver>();
var ctx = new DefaultRelayContext(scope.ServiceProvider);
ctx.Properties["tier"] = "premium";
var payment = resolver.Resolve<IPaymentService>(ctx);   // → PremiumPayment

// Factories can pick a key from the context too
services.AddRelayFactory<IPaymentService>(f => f
    .RegisterKeyedRelay<StripeRelay>("stripe")
    .RegisterKeyedRelay<PayPalRelay>("paypal")
    .SelectKeyByContext(c => (string)c.Properties["provider"])
    .SetDefaultRelay("stripe")
).Build();
var svc = factory.CreateRelay(ctx);   // key chosen from ctx.Properties["provider"]
```

### **11. Native Keyed Services**
Register relays against a service key using built-in .NET keyed DI — resolve them with `[FromKeyedServices]` or `GetRequiredKeyedService`.
```csharp
services.AddKeyedRelay<IPaymentService, StripeRelay>("stripe");
services.AddKeyedRelay<IPaymentService, PayPalRelay>("paypal");

// Resolve directly from the container
var stripe = serviceProvider.GetRequiredKeyedService<IPaymentService>("stripe");

// ...or inject by key
public class CheckoutController([FromKeyedServices("paypal")] IPaymentService payment) { }
```

### **12. Built-in Observability**
Adapter chains and multi-relays emit `System.Diagnostics.Activity` traces from an `ActivitySource` named `Relay`. Activities are only created when a listener is attached, so overhead is zero otherwise.
```csharp
// OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t.AddSource(RelayDiagnostics.SourceName));
```

## 🎯 **Real-World Use Cases**

### **1. Multi-Environment Deployments**
- **Development**: Mock services for fast development
- **Staging**: Test services with real integrations
- **Production**: Production services with monitoring

### **2. Legacy System Modernization**
- Gradual migration from old to new systems
- Bridge incompatible interfaces
- Maintain backward compatibility

### **3. Multi-Provider Integration**
- Payment processors (Stripe, PayPal, Square)
- Cloud storage (AWS, Azure, Google Cloud)
- Authentication providers (Auth0, Azure AD, Custom)

### **4. Data Processing Pipelines**
- Raw → Validated → Enriched → Processed
- Multi-step transformations with error handling
- Complex business logic workflows

### **5. Multi-Channel Broadcasting**
- Notifications (Email, SMS, Push, Slack)
- Logging (Console, File, Database, Cloud)
- Caching (Memory, Redis, Database)

## 🏗️ **Architecture Benefits**

### **✅ Clean Separation of Concerns**
- Each adapter/relay has a single responsibility
- Clear interfaces between components
- Easy to test and maintain

### **✅ Flexible Configuration**
- Runtime decision making
- Environment-specific implementations
- Feature flag support

### **✅ Performance Optimized**
- Efficient service resolution
- Proper lifetime management
- Minimal overhead

### **✅ Enterprise Ready**
- Thread-safe operations
- Comprehensive error handling
- Built-in distributed tracing via `ActivitySource`

## 🧪 **Testing Support**

```csharp
// Easy mocking for tests
services.AddConditionalRelay<IPaymentService>()
    .When(ctx => ctx.Properties["TestMode"].Equals("Mock"))
        .RelayTo<MockPaymentService>()
    .When(ctx => ctx.Properties["TestMode"].Equals("Integration"))
        .RelayTo<TestPaymentService>()
    .Build();
```

## 🔧 **Installation & Setup**

### **Package Installation**
```bash
# Package Manager
Install-Package Relay

# .NET CLI
dotnet add package Relay

# PackageReference
<PackageReference Include="Relay" Version="1.1.0" />
```

### **Basic Setup**
```csharp
using Relay;

public void ConfigureServices(IServiceCollection services)
{
    // Add relay services
    services.AddRelayServices();
    
    // Configure your relays
    services.AddRelay(config => config
        .FromAssemblyOf<IPaymentService>()
        .RegisterRelays()
    );
}
```

## 📚 **Documentation**

Runnable samples for every feature live in the [`examples/`](examples) directory — including
basic relays, conditional routing, multi-relay strategies, adapter chains, async chains, and
factories. The [`tests/`](tests) project doubles as executable documentation of the full API.

## 🤝 **Contributing**

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

## 📄 **License**

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🌟 **Support**

If you find this project helpful, please consider:
- ⭐ Starring the repository
- 🐛 Reporting issues
- 💡 Suggesting new features
- 📖 Improving documentation

---

**Relay** transforms dependency injection from simple service registration into a powerful architectural pattern for building maintainable, scalable .NET applications! 🚀
