using Xunit;

// Disable parallel test execution because [TestFixture] doesn't
// support this in general.

[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]