using System;
using System.Linq;
using StrategyGame.Core.Catalog;
using StrategyGame.Core.Services;
using Xunit;

namespace StrategyGame.Tests;

public sealed class CardCatalogTests
{
    [Fact]
    public void PrototypeUpgrade_InheritsParentMetadataAndRequirements()
    {
        var catalog = new CardCatalog();

        var def = Assert.IsType<BuildingDefinition>(catalog.Get("example_building_settlement_l2"));

        Assert.Equal(2, def.Level);
        Assert.False(def.EnabledInGame);
        Assert.Contains(def.Tags, tag => string.Equals(tag, "settlement", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(def.Tags, tag => string.Equals(tag, "population-center", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(def.Tags, tag => string.Equals(tag, "settlement-upgrade", StringComparison.OrdinalIgnoreCase));

        Assert.Contains(def.PlacementRequirements.CellTags,
            tag => string.Equals(tag, "terrain:plains", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(def.PlacementRequirements.CellTags,
            tag => string.Equals(tag, "settlement", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(def.PlacementRequirements.TargetCard);
        Assert.Equal("example_building_settlement_l1", def.PlacementRequirements.TargetCard!.DefinitionId);
        Assert.Equal(1, def.PlacementRequirements.TargetCard.MinLevel);
        Assert.Equal(2, def.PlacementRequirements.Technology["governance"]);
    }

    [Fact]
    public void PlayableCatalog_ExcludesPrototypeDefinitions()
    {
        var catalog = new CardCatalog();

        Assert.DoesNotContain(catalog.LandCards, d => d.Id == "example_land_plains");
        Assert.DoesNotContain(catalog.BuildingCards, d => d.Id == "example_building_settlement_l1");
        Assert.DoesNotContain(catalog.BuildingCards, d => d.Id == "example_building_settlement_l2");
        Assert.Contains(catalog.All, d => d.Id == "example_building_settlement_l2");
    }

    [Fact]
    public void Invest_RejectsPrototypeOnlyDefinition()
    {
        var service = new GameService(new InMemoryGameRepository(), new CardCatalog());
        var game = service.StartGame("Alice");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            service.Invest(game.GameId, "example_building_settlement_l2"));

        Assert.Contains("prototype/example definition", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}