using Xunit;
using TestSupport; // <-- this is where RunUniqueFixture lives

namespace Editor.Tests
{
    // Bind BOTH fixtures to the same collection name used by your tests.
    [CollectionDefinition("WP EndToEnd")]
    public class WpEndToEndCollection
        : ICollectionFixture<WordPressCleanupFixture>,
          ICollectionFixture<RunUniqueFixture>
    {
        // no code needed
    }
}
