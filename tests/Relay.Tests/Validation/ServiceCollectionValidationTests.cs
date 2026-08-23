using Microsoft.Extensions.DependencyInjection;
using Relay.Validation;
using Shouldly;

namespace Relay.Tests.Validation;

public class ServiceCollectionValidationTests
{
    [Fact]
    public void Validate_ShouldReportSingletonCapturingScopedDependency()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<BackgroundWorker>();

        var result = services.ValidateRelayRegistrations();

        result.IsValid.ShouldBeFalse();
        var issue = result.Errors.ShouldHaveSingleItem();
        issue.Kind.ShouldBe(RelayValidationIssueKind.CaptiveDependency);
        issue.ImplementationType.ShouldBe(typeof(BackgroundWorker));
        issue.DependencyType.ShouldBe(typeof(IUnitOfWork));
        issue.DependencyLifetime.ShouldBe(ServiceLifetime.Scoped);
    }

    [Fact]
    public void Validate_ShouldReportCaptiveDependencyBehindAKeyedRegistration()
    {
        var services = new ServiceCollection();
        services.AddKeyedScoped<IUnitOfWork, UnitOfWork>("primary");
        services.AddSingleton<KeyedBackgroundWorker>();

        var result = services.ValidateRelayRegistrations();

        result.Errors.ShouldContain(i =>
            i.Kind == RelayValidationIssueKind.CaptiveDependency
            && i.ImplementationType == typeof(KeyedBackgroundWorker)
        );
    }

    [Fact]
    public void Validate_ShouldReportMissingRegistration()
    {
        var services = new ServiceCollection();
        services.AddScoped<NeedsMissingDependency>();

        var result = services.ValidateRelayRegistrations();

        var issue = result.Errors.ShouldHaveSingleItem();
        issue.Kind.ShouldBe(RelayValidationIssueKind.MissingRegistration);
        issue.DependencyType.ShouldBe(typeof(IUnitOfWork));
    }

    [Fact]
    public void Validate_ShouldAcceptAValidGraph()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ScopedConsumer>();
        services.AddSingleton<INotifier, Notifier>();

        var result = services.ValidateRelayRegistrations();

        result.IsValid.ShouldBeTrue(result.ToString());
        result.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ShouldSatisfyClosedGenericsFromOpenGenericRegistrations()
    {
        var services = new ServiceCollection();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<RepositoryConsumer>();

        var result = services.ValidateRelayRegistrations();

        result.Issues.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_ShouldTreatCollectionsAndOptionalParametersAsResolvable()
    {
        var services = new ServiceCollection();
        services.AddScoped<CollectionConsumer>();

        var result = services.ValidateRelayRegistrations();

        result.IsValid.ShouldBeTrue(result.ToString());
    }

    [Fact]
    public void Validate_ShouldWarnAboutSingletonCapturingDisposableTransient()
    {
        var services = new ServiceCollection();
        services.AddTransient<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<BackgroundWorker>();

        var result = services.ValidateRelayRegistrations();

        result.IsValid.ShouldBeTrue("a captured transient still resolves");
        result
            .Warnings.ShouldHaveSingleItem()
            .Kind.ShouldBe(RelayValidationIssueKind.DisposableTransientCapture);
    }

    [Fact]
    public void Validate_ShouldReportDuplicatesOnlyWhenAskedTo()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.ValidateRelayRegistrations().Issues.ShouldBeEmpty();

        var result = services.ValidateRelayRegistrations(
            new RelayValidationOptions { CheckDuplicateRegistrations = true }
        );

        result
            .Warnings.ShouldHaveSingleItem()
            .Kind.ShouldBe(RelayValidationIssueKind.DuplicateRegistration);
    }

    [Fact]
    public void Validate_ShouldRespectIgnoredTypes()
    {
        var services = new ServiceCollection();
        services.AddScoped<NeedsMissingDependency>();

        var result = services.ValidateRelayRegistrations(
            new RelayValidationOptions { IgnoredServiceTypes = [typeof(IUnitOfWork)] }
        );

        result.IsValid.ShouldBeTrue(result.ToString());
    }

    [Fact]
    public void Validate_ShouldRespectTheIgnorePredicate()
    {
        var services = new ServiceCollection();
        services.AddScoped<NeedsMissingDependency>();

        var result = services.ValidateRelayRegistrations(
            new RelayValidationOptions { Ignore = type => type == typeof(NeedsMissingDependency) }
        );

        result.IsValid.ShouldBeTrue(result.ToString());
    }

    [Fact]
    public void Validate_ShouldIgnoreFactoryAndInstanceRegistrations()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INotifier>(_ => new Notifier());
        services.AddSingleton(new Notifier());

        services.ValidateRelayRegistrations().Issues.ShouldBeEmpty();
    }

    [Fact]
    public void ValidateOrThrow_ShouldThrowWithEveryIssueInTheMessage()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<BackgroundWorker>();

        var exception = Should.Throw<RelayValidationException>(() =>
            services.ValidateRelayRegistrationsOrThrow()
        );

        exception.Result.Errors.ShouldNotBeEmpty();
        exception.Message.ShouldContain(nameof(BackgroundWorker));
    }

    [Fact]
    public void ValidateOrThrow_ShouldReturnTheCollectionWhenValid()
    {
        var services = new ServiceCollection();
        services.AddSingleton<INotifier, Notifier>();

        services.ValidateRelayRegistrationsOrThrow().ShouldBeSameAs(services);
    }

    [Fact]
    public void Validate_ShouldThrowOnNullServices()
    {
        Should.Throw<ArgumentNullException>(() =>
            ((IServiceCollection)null!).ValidateRelayRegistrations()
        );
    }

    [Fact]
    public void Result_ToString_ShouldDescribeAnEmptyRun()
    {
        new RelayValidationResult([]).ToString().ShouldBe("No dependency injection issues found.");
    }

    private interface IUnitOfWork;

    private sealed class UnitOfWork : IUnitOfWork, IDisposable
    {
        public void Dispose() { }
    }

    private interface INotifier;

    private sealed class Notifier : INotifier;

    private interface IRepository<T>;

    private sealed class Repository<T> : IRepository<T>;

    private sealed class BackgroundWorker(IUnitOfWork unitOfWork)
    {
        public IUnitOfWork UnitOfWork { get; } = unitOfWork;
    }

    private sealed class KeyedBackgroundWorker(
        [FromKeyedServices("primary")] IUnitOfWork unitOfWork
    )
    {
        public IUnitOfWork UnitOfWork { get; } = unitOfWork;
    }

    private sealed class ScopedConsumer(IUnitOfWork unitOfWork, INotifier notifier)
    {
        public IUnitOfWork UnitOfWork { get; } = unitOfWork;
        public INotifier Notifier { get; } = notifier;
    }

    private sealed class RepositoryConsumer(IRepository<string> repository)
    {
        public IRepository<string> Repository { get; } = repository;
    }

    private sealed class NeedsMissingDependency(IUnitOfWork unitOfWork)
    {
        public IUnitOfWork UnitOfWork { get; } = unitOfWork;
    }

    private sealed class CollectionConsumer(
        IEnumerable<INotifier> notifiers,
        IServiceProvider provider,
        string name = "default"
    )
    {
        public IEnumerable<INotifier> Notifiers { get; } = notifiers;
        public IServiceProvider Provider { get; } = provider;
        public string Name { get; } = name;
    }
}
