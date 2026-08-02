using Xunit;

namespace NoraBar.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WpfApplicationCollection
{
    public const string Name = nameof(WpfApplicationCollection);
}
