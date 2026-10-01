using Microsoft.Data.Sqlite;
using SupplierPurchases.Data;
using SupplierPurchases.Models;

namespace SupplierPurchases.Tests;

public sealed class AppDatabaseTests : IDisposable
{
    private static readonly DateTime OrderDate = new(2026, 9, 10);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "SupplierPurchasesTests", Guid.NewGuid().ToString("N"));
    private readonly AppDatabase _database;

    public AppDatabaseTests()
    {
        _database = new AppDatabase(Path.Combine(_directory, "test.db"));
        _database.EnsureCreated();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_directory, recursive: true);
    }

    private static OrderItemRow Item(string product, double quantity, string unit, string supplier) =>
        new() { ProductName = product, Quantity = quantity, Unit = unit, SupplierName = supplier };

    [Fact]
    public void GetPurchaseList_SumsSameProductAndUnitAcrossBranches()
    {
        _database.SaveOrder(null, "فرع أ", OrderDate, "", [Item("اير بار", 5, "حبه", "البيرق")]);
        _database.SaveOrder(null, "فرع ب", OrderDate, "", [Item("  اير   بار ", 3, "حبه", "البيرق")]);

        var item = Assert.Single(_database.GetPurchaseList("البيرق", OrderDate));

        Assert.Equal(8, item.TotalQuantity);
        Assert.Equal("فرع أ: 5، فرع ب: 3", item.Sources);
    }

    [Fact]
    public void GetPurchaseList_DifferentUnits_StaySeparate()
    {
        _database.SaveOrder(null, "فرع أ", OrderDate, "",
        [
            Item("اير بار", 5, "حبه", "البيرق"),
            Item("اير بار", 2, "كرز", "البيرق")
        ]);

        Assert.Equal(2, _database.GetPurchaseList("البيرق", OrderDate).Count);
    }

    [Fact]
    public void GetPurchaseList_OtherSupplierAndDate_AreExcluded()
    {
        _database.SaveOrder(null, "فرع أ", OrderDate, "", [Item("اير بار", 5, "حبه", "مورد اخر")]);
        _database.SaveOrder(null, "فرع أ", OrderDate.AddDays(1), "", [Item("اير بار", 5, "حبه", "البيرق")]);

        Assert.Empty(_database.GetPurchaseList("البيرق", OrderDate));
    }

    [Fact]
    public void SaveOrder_ProductWithoutSupplier_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _database.SaveOrder(null, "فرع أ", OrderDate, "", [Item("اير بار", 5, "حبه", "")]));
    }

    [Fact]
    public void SaveOrder_RemembersLastUnitPerProduct()
    {
        _database.SaveOrder(null, "فرع أ", OrderDate, "", [Item("اير بار", 5, "كرز", "البيرق")]);

        var units = _database.GetProductDefaultUnits();

        Assert.Equal("كرز", units[AppDatabase.NormalizeName("اير بار")]);
    }

    [Fact]
    public void SupplierRules_UnitRoundTrips()
    {
        _database.SaveSupplierRules(
        [
            new SupplierRuleRow { Keyword = "اير بار", SupplierName = "البيرق", Unit = "كرز" },
            new SupplierRuleRow { Keyword = "بار", SupplierName = "مورد", Unit = "قيمة غير صالحة" }
        ]);

        var rules = _database.GetSupplierRules().ToDictionary(rule => rule.Keyword, rule => rule.Unit);

        Assert.Equal("كرز", rules["اير بار"]);
        Assert.Equal(string.Empty, rules["بار"]);
    }

    [Fact]
    public void GetOrderYears_ReturnsDistinctYearsNewestFirst()
    {
        _database.SaveOrder(null, "فرع أ", new DateTime(2025, 3, 1), "", [Item("منتج", 1, "حبه", "مورد")]);
        _database.SaveOrder(null, "فرع أ", new DateTime(2026, 3, 1), "", [Item("منتج", 1, "حبه", "مورد")]);
        _database.SaveOrder(null, "فرع ب", new DateTime(2026, 5, 1), "", [Item("منتج", 1, "حبه", "مورد")]);

        Assert.Equal([2026, 2025], _database.GetOrderYears());
    }

    [Fact]
    public void EnsureCreated_OldRulesTable_GetsUnitColumn()
    {
        var oldDbPath = Path.Combine(_directory, "old.db");
        using (var connection = new SqliteConnection($"Data Source={oldDbPath}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE supplier_rules (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    keyword TEXT NOT NULL,
                    keyword_key TEXT NOT NULL,
                    supplier_name TEXT NOT NULL,
                    supplier_key TEXT NOT NULL,
                    priority INTEGER NOT NULL DEFAULT 0
                );
                INSERT INTO supplier_rules (keyword, keyword_key, supplier_name, supplier_key, priority)
                VALUES ('بار', 'بار', 'مورد', 'مورد', 0);
                """;
            command.ExecuteNonQuery();
        }

        var oldDatabase = new AppDatabase(oldDbPath);
        oldDatabase.EnsureCreated();

        var rule = Assert.Single(oldDatabase.GetSupplierRules());
        Assert.Equal(string.Empty, rule.Unit);
    }
}
