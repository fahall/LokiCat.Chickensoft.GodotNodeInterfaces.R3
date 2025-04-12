using FluentAssertions;

namespace ObservableGenerator.Tests;

public class TestsShould
{
    [Fact]
    public void PassWhenTrue()
    {
        true.Should().BeTrue();
    }
}