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
- **🏭 Relay Factories**: Key-based service creation with flexible configuration
- **⚡ Performance Optimized**: Efficient resolution with comprehensive lifetime management

## 🚀 **Quick Start**

### Installation
```bash
dotnet add package Relay
```

### Basic Usage
```csharp
using Relay;

// Configure services
services.AddRelayServices();

// Basic relay
services.AddRelay<IPaymentService, StripePaymentRelay>()
    .WithScopedLifetime();

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
    .DecorateWith<LoggingDecorator>();
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
// Your RefactoringGuru example becomes:
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

## 🔧 **Advanced Features**

### **Conditional Chain Selection**
```csharp
services.AddConditionalAdapterChain<IUserService>()
    .When(ctx => ctx.Environment == "Development")
    .UseChain(chain => chain
        .From<MockDatabase>()
        .Finally<MockUserService>())
    .When(ctx => ctx.Environment == "Production") 
    .UseChain(chain => chain
        .From<LegacyDatabase>()
        .Then<IModernOrm, LegacyToOrmAdapter>()
        .Then<IRepository<User>, OrmToRepositoryAdapter<User>>()
        .Finally<RepositoryToServiceAdapter>())
    .Build();
```

### **Parallel Chain Execution**
```csharp
services.AddParallelAdapterChains<IDataProcessor>()
    .AddChain("fast", chain => chain
        .From<RawData>()
        .Then<CachedData, FastCacheAdapter>()
        .Finally<FastProcessorAdapter>())
    .AddChain("thorough", chain => chain
        .From<RawData>()
        .Then<ValidatedData, ValidationAdapter>()
        .Then<EnrichedData, EnrichmentAdapter>()
        .Finally<ThoroughProcessorAdapter>())
    .Build();

// Usage
var chainFactory = serviceProvider.GetService<IAdapterChainFactory<IDataProcessor>>();
var processor = chainFactory.CreateFromChain("thorough");
```

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
- Extensive logging support

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
<PackageReference Include="Relay" Version="1.0.3" />
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

todo:

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
