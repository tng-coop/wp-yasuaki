using TestSupport;
using Xunit;

// Per-assembly binding: ties WP EndToEnd collection to RunUniqueFixture
[CollectionDefinition("WP EndToEnd", DisableParallelization = true)]
public class WpEndToEndCollection : ICollectionFixture<RunUniqueFixture> { }
