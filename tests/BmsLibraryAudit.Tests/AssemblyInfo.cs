using Xunit;

// Keep disk I/O and junction fixtures serial; large libraries are also scanned serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
