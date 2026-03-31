using System.Text.Json;
using StrategyGame.Core.Models;
using Xunit;

namespace StrategyGame.Tests;

public sealed class ResourceAmountTests
{
    [Fact]
    public void JsonDeserializer_ReadsArbitraryResourceIds()
    {
        var resources = JsonSerializer.Deserialize<ResourceAmount>("{\"gold\":100,\"wood\":40,\"sugar\":10}");

        Assert.NotNull(resources);
        Assert.Equal(40, resources!.Wood);
        Assert.Equal(100, resources.Get("gold"));
        Assert.Equal(10, resources.Get("sugar"));
    }

    [Fact]
    public void CanAfford_UsesExtraResources()
    {
        var supply = new ResourceAmount(Wood: 40).Set("gold", 100).Set("sugar", 10);
        var cost = ResourceAmount.Zero.Set("gold", 50).Set("sugar", 5);

        Assert.True(supply.CanAfford(cost));
        Assert.False(cost.CanAfford(supply));
    }

    [Fact]
    public void AddAndSubtract_PreserveExtraResources()
    {
        var first = ResourceAmount.Zero.Set("gold", 100).Set("sugar", 7);
        var second = ResourceAmount.Zero.Set("gold", 30).Set("sugar", 2);

        var sum = first.Add(second);
        var diff = sum.Subtract(second);

        Assert.Equal(130, sum.Get("gold"));
        Assert.Equal(9, sum.Get("sugar"));
        Assert.Equal(100, diff.Get("gold"));
        Assert.Equal(7, diff.Get("sugar"));
    }
}