using SupplierPurchases.Data;
using SupplierPurchases.Models;

namespace SupplierPurchases.Tests;

public class SupplierRuleEngineTests
{
    private static SupplierRuleEngine EngineWith(params SupplierRuleRow[] rules)
    {
        var engine = new SupplierRuleEngine();
        engine.SetRules(rules);
        return engine;
    }

    [Fact]
    public void FindMatch_KeywordWithSpellingVariant_Matches()
    {
        var engine = EngineWith(new SupplierRuleRow { Keyword = "اير بار", SupplierName = "البيرق" });

        var match = engine.FindMatch("نكهة إير بار ليمون");

        Assert.Equal("البيرق", match?.SupplierName);
    }

    [Fact]
    public void FindMatch_NoKeywordInProduct_ReturnsNull()
    {
        var engine = EngineWith(new SupplierRuleRow { Keyword = "اير بار", SupplierName = "البيرق" });

        Assert.Null(engine.FindMatch("منتج اخر"));
    }

    [Fact]
    public void FindMatch_HigherPriorityWins()
    {
        var engine = EngineWith(
            new SupplierRuleRow { Keyword = "بار", SupplierName = "مورد أ", Priority = 1 },
            new SupplierRuleRow { Keyword = "اير بار", SupplierName = "مورد ب", Priority = 0 });

        Assert.Equal("مورد أ", engine.FindMatch("اير بار")?.SupplierName);
    }

    [Fact]
    public void FindMatch_SamePriority_LongerKeywordWins()
    {
        var engine = EngineWith(
            new SupplierRuleRow { Keyword = "بار", SupplierName = "مورد أ" },
            new SupplierRuleRow { Keyword = "اير بار", SupplierName = "مورد ب" });

        Assert.Equal("مورد ب", engine.FindMatch("اير بار")?.SupplierName);
    }

    [Theory]
    [InlineData("كرز", "كرز")]
    [InlineData("حبه", "حبه")]
    [InlineData("", "")]
    [InlineData("كرتون", "")]
    public void FindMatch_ReturnsOnlyValidUnits(string ruleUnit, string expectedUnit)
    {
        var engine = EngineWith(new SupplierRuleRow { Keyword = "بار", SupplierName = "مورد", Unit = ruleUnit });

        Assert.Equal(expectedUnit, engine.FindMatch("بار")?.Unit);
    }

    [Fact]
    public void SetRules_SkipsRulesWithoutKeywordOrSupplier()
    {
        var engine = EngineWith(
            new SupplierRuleRow { Keyword = " ", SupplierName = "مورد" },
            new SupplierRuleRow { Keyword = "بار", SupplierName = "" });

        Assert.Equal(0, engine.Count);
    }
}
