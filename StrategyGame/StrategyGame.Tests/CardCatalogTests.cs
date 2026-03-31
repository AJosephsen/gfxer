using System;
using System.Linq;
using StrategyGame.Core.Models.Cards;
using StrategyGame.Core.Catalog;
using StrategyGame.Core.Models;
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
    public void PlayCard_Upgrade_ReplacesTargetBuildingWhenRequirementsMet()
    {
        var repo = new InMemoryGameRepository();
        var service = new GameService(repo, new CardCatalog());
        var game = service.StartGame("Alice");
        var cell = game.Board.AllCells().First(c => c.IsEmpty);

        cell.Land = LandCard.Create((LandDefinition)new CardCatalog().Get("land_plains"));
        cell.Building = BuildingCard.Create("example_building_settlement_l1", 1);
        game.Technologies["governance"] = 2;
        game.Resources = game.Resources with { Wood = 100, Flux = ResourceAmount.MaxFlux };
        game.Hand.Clear();

        var upgrade = BuildingCard.Create("example_building_settlement_l2", 2);
        game.Hand.Add(upgrade);
        repo.Save(game);

        var (updated, message) = service.PlayCard(game.GameId, upgrade.InstanceId, cell.Row, cell.Col);

        var upgraded = updated.Board.GetCell(cell.Row, cell.Col).Building;
        Assert.NotNull(upgraded);
        Assert.Equal("example_building_settlement_l2", upgraded!.DefinitionId);
        Assert.Equal(2, upgraded.Level);
        Assert.Contains("Upgraded", message);
    }
}