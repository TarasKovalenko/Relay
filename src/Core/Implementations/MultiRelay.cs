using Relay.Core.Enums;
using Relay.Core.Interfaces;

namespace Relay.Core.Implementations;

public sealed class MultiRelay<TInterface> : IMultiRelay<TInterface>
    where TInterface : class
{
    private readonly List<TInterface> _relays;
    private readonly RelayStrategy _strategy;
    private int _roundRobinIndex;
    private readonly object _lockObject = new();

    public MultiRelay(IEnumerable<TInterface> relays, RelayStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(relays);
        _relays = relays.ToList();
        _strategy = strategy;
    }

    public IEnumerable<TInterface> GetRelays() => _relays;

    public async Task<IEnumerable<TResult>> RelayToAllWithResults<TResult>(
        Func<TInterface, Task<TResult>> operation
    )
    {
        ArgumentNullException.ThrowIfNull(operation);

        return _strategy switch
        {
            RelayStrategy.Broadcast => await ExecuteBroadcast(operation),
            RelayStrategy.Failover => [await ExecuteFailover(operation)],
            RelayStrategy.FirstSuccessful => [await ExecuteFirstSuccessful(operation)],
            RelayStrategy.Parallel => await ExecuteParallel(operation),
            RelayStrategy.RoundRobin => [await operation(await GetNextRelay())],
            _ => throw new NotSupportedException($"Strategy {_strategy} not supported for results"),
        };
    }

    public async Task RelayToAll(Func<TInterface, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        switch (_strategy)
        {
            case RelayStrategy.Broadcast:
                foreach (var relay in _relays)
                {
                    await operation(relay);
                }
                break;
            case RelayStrategy.Failover:
            case RelayStrategy.FirstSuccessful:
                await ExecuteFirstSuccessful(operation);
                break;
            case RelayStrategy.Parallel:
                await Task.WhenAll(_relays.Select(operation));
                break;
            case RelayStrategy.RoundRobin:
                await operation(await GetNextRelay());
                break;
            default:
                throw new NotSupportedException($"Strategy {_strategy} not supported");
        }
    }

    public Task<TInterface> GetNextRelay()
    {
        if (_relays.Count == 0)
        {
            throw new InvalidOperationException("No relays available");
        }

        lock (_lockObject)
        {
            var relay = _relays[_roundRobinIndex % _relays.Count];
            _roundRobinIndex++;
            return Task.FromResult(relay);
        }
    }

    private async Task<IEnumerable<TResult>> ExecuteBroadcast<TResult>(
        Func<TInterface, Task<TResult>> operation
    )
    {
        var results = new List<TResult>();
        foreach (var relay in _relays)
        {
            results.Add(await operation(relay));
        }
        return results;
    }

    private async Task<TResult> ExecuteFirstSuccessful<TResult>(
        Func<TInterface, Task<TResult>> operation
    )
    {
        Exception? lastException = null;
        foreach (var relay in _relays)
        {
            try
            {
                return await operation(relay);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }
        throw new InvalidOperationException("No relay succeeded", lastException);
    }

    private async Task ExecuteFirstSuccessful(Func<TInterface, Task> operation)
    {
        Exception? lastException = null;
        foreach (var relay in _relays)
        {
            try
            {
                await operation(relay);
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }
        throw new InvalidOperationException("No relay succeeded", lastException);
    }

    private async Task<TResult> ExecuteFailover<TResult>(Func<TInterface, Task<TResult>> operation)
    {
        return await ExecuteFirstSuccessful(operation);
    }

    private async Task<IEnumerable<TResult>> ExecuteParallel<TResult>(
        Func<TInterface, Task<TResult>> operation
    )
    {
        var tasks = _relays.Select(operation).ToArray();
        return await Task.WhenAll(tasks);
    }
}
