using Xunit;

// Relay publishes its metrics on a single static Meter, so a MetricCollector in one test would
// otherwise observe measurements produced by tests running in parallel with it. The whole suite
// finishes in well under a second, so serialising it costs nothing.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
