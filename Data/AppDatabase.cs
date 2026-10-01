using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using SupplierPurchases.Models;

namespace SupplierPurchases.Data;

public sealed class AppDatabase
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string SeedFlagKey = "products_seeded";
    private readonly string _dbPath;

    public AppDatabase()
    {
        var appDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SupplierPurchases");

        Directory.CreateDirectory(appDirectory);
        _dbPath = Path.Combine(appDirectory, "supplier_purchases.db");
    }

    public string DatabasePath => _dbPath;

    public void EnsureCreated()
    {
        if (File.Exists(_dbPath))
        {
            CreateDailyBackup();
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS branches (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                normalized_name TEXT NOT NULL UNIQUE,
                notes TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS orders (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                branch_id INTEGER NOT NULL,
                order_date TEXT NOT NULL,
                notes TEXT NOT NULL DEFAULT '',
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (branch_id) REFERENCES branches(id)
            );

            CREATE TABLE IF NOT EXISTS order_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                order_id INTEGER NOT NULL,
                product_name TEXT NOT NULL,
                product_key TEXT NOT NULL,
                quantity REAL NOT NULL,
                unit TEXT NOT NULL,
                unit_key TEXT NOT NULL,
                supplier_name TEXT NOT NULL,
                supplier_key TEXT NOT NULL,
                notes TEXT NOT NULL DEFAULT '',
                FOREIGN KEY (order_id) REFERENCES orders(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS saved_products (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                normalized_name TEXT NOT NULL UNIQUE,
                default_unit TEXT NOT NULL DEFAULT '',
                last_used_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS saved_suppliers (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                normalized_name TEXT NOT NULL UNIQUE,
                phone TEXT NOT NULL DEFAULT '',
                notes TEXT NOT NULL DEFAULT '',
                last_used_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS supplier_rules (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                keyword TEXT NOT NULL,
                keyword_key TEXT NOT NULL,
                supplier_name TEXT NOT NULL,
                supplier_key TEXT NOT NULL,
                unit TEXT NOT NULL DEFAULT '',
                priority INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS app_meta (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_orders_branch ON orders(branch_id);
            CREATE INDEX IF NOT EXISTS idx_items_order ON order_items(order_id);
            CREATE INDEX IF NOT EXISTS idx_items_supplier ON order_items(supplier_key);
            CREATE INDEX IF NOT EXISTS idx_items_product_supplier_unit ON order_items(product_key, supplier_key, unit_key);
            """;
        command.ExecuteNonQuery();

        // قواعد الإسناد في القواعد القديمة لم يكن لها عمود الوحدة
        if (!ColumnExists(connection, "supplier_rules", "unit"))
        {
            Execute(connection, null, "ALTER TABLE supplier_rules ADD COLUMN unit TEXT NOT NULL DEFAULT '';");
        }

        // تُضاف قائمة المنتجات الجاهزة مرة واحدة فقط، حتى لا تعود المنتجات المحذوفة بعد إعادة التشغيل
        if (ReadMetaValue(connection, SeedFlagKey) is null)
        {
            ImportProductNames(ProductSeeder.SeedProducts);
            WriteMetaValue(connection, SeedFlagKey, DateTime.Now.ToString("s"));
        }
    }

    public DashboardStats GetDashboardStats()
    {
        using var connection = OpenConnection();

        return new DashboardStats
        {
            OrderCount = ExecuteScalarInt(connection, "SELECT COUNT(*) FROM orders;"),
            SupplierCount = ExecuteScalarInt(connection, "SELECT COUNT(DISTINCT supplier_key) FROM order_items WHERE supplier_key <> '';"),
            ProductCount = ExecuteScalarInt(connection, "SELECT COUNT(DISTINCT product_key || '|' || supplier_key || '|' || unit_key) FROM order_items WHERE product_key <> '';")
        };
    }

    public List<OrderSummary> GetOrderSummaries()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT o.id, b.name AS branch_name, o.order_date, o.notes, COUNT(oi.id) AS item_count
            FROM orders o
            INNER JOIN branches b ON b.id = o.branch_id
            LEFT JOIN order_items oi ON oi.order_id = o.id
            GROUP BY o.id, b.name, o.order_date, o.notes
            ORDER BY o.order_date DESC, o.id DESC;
            """;

        using var reader = command.ExecuteReader();
        var orders = new List<OrderSummary>();
        while (reader.Read())
        {
            orders.Add(new OrderSummary
            {
                Id = reader.GetInt32(0),
                BranchName = reader.GetString(1),
                OrderDate = ParseDate(reader.GetString(2)),
                Notes = reader.GetString(3),
                ItemCount = reader.GetInt32(4)
            });
        }

        return orders;
    }

    public OrderDetail? GetOrderDetail(int orderId)
    {
        using var connection = OpenConnection();
        using var orderCommand = connection.CreateCommand();
        orderCommand.CommandText = """
            SELECT o.id, b.name, o.order_date, o.notes
            FROM orders o
            INNER JOIN branches b ON b.id = o.branch_id
            WHERE o.id = @id;
            """;
        orderCommand.Parameters.AddWithValue("@id", orderId);

        using var orderReader = orderCommand.ExecuteReader();
        if (!orderReader.Read())
        {
            return null;
        }

        var detail = new OrderDetail
        {
            Id = orderReader.GetInt32(0),
            BranchName = orderReader.GetString(1),
            OrderDate = ParseDate(orderReader.GetString(2)),
            Notes = orderReader.GetString(3)
        };

        orderReader.Close();

        using var itemsCommand = connection.CreateCommand();
        itemsCommand.CommandText = """
            SELECT id, product_name, quantity, unit, supplier_name, notes
            FROM order_items
            WHERE order_id = @orderId
            ORDER BY id;
            """;
        itemsCommand.Parameters.AddWithValue("@orderId", orderId);

        using var itemsReader = itemsCommand.ExecuteReader();
        while (itemsReader.Read())
        {
            detail.Items.Add(new OrderItemRow
            {
                Id = itemsReader.GetInt32(0),
                ProductName = itemsReader.GetString(1),
                RuleCheckedProduct = itemsReader.GetString(1),
                Quantity = itemsReader.GetDouble(2),
                Unit = itemsReader.GetString(3),
                SupplierName = itemsReader.GetString(4),
                Notes = itemsReader.GetString(5)
            });
        }

        return detail;
    }

    public void SaveOrder(int? orderId, string branchName, DateTime orderDate, string notes, IEnumerable<OrderItemRow> items)
    {
        var cleanBranch = CleanName(branchName);
        if (string.IsNullOrWhiteSpace(cleanBranch))
        {
            throw new InvalidOperationException("اسم الفرع مطلوب.");
        }

        var parsedItems = items
            .Select(item => new CleanOrderItem(
                CleanName(item.ProductName),
                item.Quantity,
                CleanName(item.Unit),
                CleanName(item.SupplierName),
                CleanName(item.Notes)))
            .ToList();

        // السطر يُعتبر مبدوءًا بمجرد كتابة اسم المنتج. الأسطر التي تحمل اسم مورد فقط
        // (مثل الناتجة عن الإسناد الجماعي لأسطر فارغة) تُتجاهل بدل رفض الحفظ.
        var hasPartialInvalidRow = items.Any(item =>
            !string.IsNullOrWhiteSpace(item.ProductName) &&
            (string.IsNullOrWhiteSpace(item.SupplierName) || item.Quantity <= 0));

        if (hasPartialInvalidRow)
        {
            throw new InvalidOperationException("يوجد منتجات بياناتها غير مكتملة. تأكد من إدخال اسم المنتج، الكمية (أكبر من 0)، واسم المورد لكل سطر تم البدء بكتابته.");
        }

        var cleanItems = parsedItems
            .Where(item => !string.IsNullOrWhiteSpace(item.ProductName)
                           && !string.IsNullOrWhiteSpace(item.SupplierName)
                           && item.Quantity > 0)
            .ToList();

        if (cleanItems.Count == 0)
        {
            throw new InvalidOperationException("أضف منتجًا واحدًا على الأقل مع كمية ومورد.");
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var branchId = UpsertBranch(connection, transaction, cleanBranch);
        var savedOrderId = orderId ?? InsertOrder(connection, transaction, branchId, orderDate, notes);

        if (orderId.HasValue)
        {
            UpdateOrder(connection, transaction, orderId.Value, branchId, orderDate, notes);
            DeleteOrderItems(connection, transaction, orderId.Value);
        }

        foreach (var item in cleanItems)
        {
            InsertOrderItem(connection, transaction, savedOrderId, item);
            UpsertSavedProduct(connection, transaction, item.ProductName, item.Unit);
            UpsertSavedSupplier(connection, transaction, item.SupplierName);
        }

        transaction.Commit();
    }

    public void DeleteOrder(int orderId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM orders WHERE id = @id;";
        command.Parameters.AddWithValue("@id", orderId);
        command.ExecuteNonQuery();
    }

    public List<string> GetSavedProducts()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM saved_products ORDER BY last_used_at DESC, name LIMIT 5000;";

        using var reader = command.ExecuteReader();
        var products = new List<string>();
        while (reader.Read())
        {
            products.Add(reader.GetString(0));
        }

        return products;
    }

    public void ImportProductNames(IEnumerable<string> names)
    {
        ImportNames(names, """
            INSERT INTO saved_products (name, normalized_name, default_unit, last_used_at)
            VALUES (@name, @key, 'حبه', @now)
            ON CONFLICT(normalized_name) DO NOTHING;
            """);
    }

    public void ImportSupplierNames(IEnumerable<string> names)
    {
        ImportNames(names, """
            INSERT INTO saved_suppliers (name, normalized_name, last_used_at)
            VALUES (@name, @key, @now)
            ON CONFLICT(normalized_name) DO NOTHING;
            """);
    }

    private void ImportNames(IEnumerable<string> names, string insertSql)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var now = DateTime.Now.ToString("s");
        foreach (var name in names)
        {
            var cleanName = CleanName(name);
            if (cleanName.Length == 0)
            {
                continue;
            }

            Execute(connection, transaction, insertSql,
                ("@name", cleanName), ("@key", NormalizeName(cleanName)), ("@now", now));
        }

        transaction.Commit();
    }

    /// <summary>يحذف منتجًا واحدًا أو أكثر من قائمة الاقتراحات داخل معاملة واحدة.</summary>
    public void DeleteSavedProducts(IEnumerable<string> productNames)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var productName in productNames)
        {
            var key = NormalizeName(productName);
            if (key.Length == 0)
            {
                continue;
            }

            Execute(connection, transaction, "DELETE FROM saved_products WHERE normalized_name = @key;", ("@key", key));
        }

        transaction.Commit();
    }

    public void UpdateSavedProduct(string oldName, string newName)
    {
        var cleanOld = CleanName(oldName);
        var cleanNew = CleanName(newName);
        if (string.IsNullOrWhiteSpace(cleanNew))
        {
            throw new InvalidOperationException("اسم المنتج الجديد لا يمكن أن يكون فارغاً.");
        }

        var oldKey = NormalizeName(cleanOld);
        var newKey = NormalizeName(cleanNew);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. تحديث اسم المنتج في جدول الاقتراحات saved_products
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = """
                    UPDATE saved_products 
                    SET name = @newName, normalized_name = @newKey 
                    WHERE normalized_name = @oldKey;
                    """;
                cmd.Parameters.AddWithValue("@newName", cleanNew);
                cmd.Parameters.AddWithValue("@newKey", newKey);
                cmd.Parameters.AddWithValue("@oldKey", oldKey);
                cmd.ExecuteNonQuery();
            }

            // 2. تحديث اسم المنتج في جميع بنود الطلبيات السابقة والحالية order_items
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = """
                    UPDATE order_items 
                    SET product_name = @newName, product_key = @newKey 
                    WHERE product_key = @oldKey;
                    """;
                cmd.Parameters.AddWithValue("@newName", cleanNew);
                cmd.Parameters.AddWithValue("@newKey", newKey);
                cmd.Parameters.AddWithValue("@oldKey", oldKey);
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }


    public List<string> GetSavedSuppliers()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name FROM saved_suppliers
            UNION
            SELECT supplier_name FROM order_items WHERE supplier_key <> ''
            ORDER BY name;
            """;

        using var reader = command.ExecuteReader();
        var suppliers = new List<string>();
        while (reader.Read())
        {
            suppliers.Add(reader.GetString(0));
        }

        return suppliers
            .GroupBy(NormalizeName)
            .Select(group => group.First())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name)
            .ToList();
    }

    /// <summary>كل الموردين المحفوظين مع عدد أسطر الطلبيات المرتبطة بكل مورد.</summary>
    public List<SupplierRow> GetSupplierRows()
    {
        using var connection = OpenConnection();

        var usage = new Dictionary<string, (string Name, int ItemCount, int ProductCount)>(StringComparer.Ordinal);
        using (var usageCommand = connection.CreateCommand())
        {
            usageCommand.CommandText = """
                SELECT supplier_key, MIN(supplier_name), COUNT(*),
                       COUNT(DISTINCT product_key || '|' || unit_key)
                FROM order_items
                WHERE supplier_key <> ''
                GROUP BY supplier_key;
                """;

            using var reader = usageCommand.ExecuteReader();
            while (reader.Read())
            {
                usage[reader.GetString(0)] = (reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3));
            }
        }

        var rows = new Dictionary<string, SupplierRow>(StringComparer.Ordinal);
        using (var savedCommand = connection.CreateCommand())
        {
            savedCommand.CommandText = "SELECT name, normalized_name FROM saved_suppliers;";

            using var reader = savedCommand.ExecuteReader();
            while (reader.Read())
            {
                var key = reader.GetString(1);
                usage.TryGetValue(key, out var stats);
                rows[key] = new SupplierRow
                {
                    Key = key,
                    Name = reader.GetString(0),
                    ItemCount = stats.ItemCount,
                    ProductCount = stats.ProductCount
                };
            }
        }

        foreach (var (key, stats) in usage)
        {
            if (rows.ContainsKey(key))
            {
                continue;
            }

            rows[key] = new SupplierRow
            {
                Key = key,
                Name = stats.Name,
                ItemCount = stats.ItemCount,
                ProductCount = stats.ProductCount
            };
        }

        return rows.Values.OrderBy(row => row.Name, StringComparer.CurrentCulture).ToList();
    }

    public int GetSupplierUsageCount(string supplierName)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM order_items WHERE supplier_key = @key;";
        command.Parameters.AddWithValue("@key", NormalizeName(supplierName));
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>يعيد تسمية المورد في كل الجداول، ويدمجه مع مورد آخر إذا كان الاسم الجديد موجودًا.</summary>
    public void RenameSupplier(string oldName, string newName)
    {
        var cleanNew = CleanName(newName);
        if (string.IsNullOrWhiteSpace(cleanNew))
        {
            throw new InvalidOperationException("اسم المورد الجديد لا يمكن أن يكون فارغاً.");
        }

        var oldKey = NormalizeName(oldName);
        var newKey = NormalizeName(cleanNew);
        if (oldKey == newKey && CleanName(oldName) == cleanNew)
        {
            return;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        if (oldKey != newKey)
        {
            Execute(connection, transaction,
                "DELETE FROM saved_suppliers WHERE normalized_name = @oldKey;",
                ("@oldKey", oldKey));
        }

        UpsertSavedSupplier(connection, transaction, cleanNew);

        Execute(connection, transaction,
            "UPDATE order_items SET supplier_name = @newName, supplier_key = @newKey WHERE supplier_key = @oldKey;",
            ("@newName", cleanNew), ("@newKey", newKey), ("@oldKey", oldKey));

        Execute(connection, transaction,
            "UPDATE supplier_rules SET supplier_name = @newName, supplier_key = @newKey WHERE supplier_key = @oldKey;",
            ("@newName", cleanNew), ("@newKey", newKey), ("@oldKey", oldKey));

        transaction.Commit();
    }

    /// <summary>يحذف الموردين من القائمة المحفوظة مع قواعد الإسناد الخاصة بهم. لا يمس أسطر الطلبيات.</summary>
    public void DeleteSuppliers(IEnumerable<string> supplierNames)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var supplierName in supplierNames)
        {
            var key = NormalizeName(supplierName);
            if (key.Length == 0)
            {
                continue;
            }

            Execute(connection, transaction, "DELETE FROM saved_suppliers WHERE normalized_name = @key;", ("@key", key));
            Execute(connection, transaction, "DELETE FROM supplier_rules WHERE supplier_key = @key;", ("@key", key));
        }

        transaction.Commit();
    }

    public List<SupplierRuleRow> GetSupplierRules()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT keyword, supplier_name, priority, unit
            FROM supplier_rules
            ORDER BY priority DESC, keyword;
            """;

        using var reader = command.ExecuteReader();
        var rules = new List<SupplierRuleRow>();
        while (reader.Read())
        {
            rules.Add(new SupplierRuleRow
            {
                Keyword = reader.GetString(0),
                SupplierName = reader.GetString(1),
                Priority = reader.GetInt32(2),
                Unit = reader.GetString(3)
            });
        }

        return rules;
    }

    /// <summary>يحفظ كل قواعد الإسناد داخل معاملة واحدة (تستبدل القواعد السابقة).</summary>
    public void SaveSupplierRules(IEnumerable<SupplierRuleRow> rules)
    {
        var validRules = rules
            .Select(rule => new
            {
                Keyword = CleanName(rule.Keyword),
                SupplierName = CleanName(rule.SupplierName),
                Unit = SupplierRuleEngine.NormalizeUnit(rule.Unit),
                rule.Priority
            })
            .Where(rule => rule.Keyword.Length > 0 && rule.SupplierName.Length > 0)
            .ToList();

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        Execute(connection, transaction, "DELETE FROM supplier_rules;");

        foreach (var rule in validRules)
        {
            Execute(connection, transaction, """
                INSERT INTO supplier_rules (keyword, keyword_key, supplier_name, supplier_key, unit, priority)
                VALUES (@keyword, @keywordKey, @supplier, @supplierKey, @unit, @priority);
                """,
                ("@keyword", rule.Keyword),
                ("@keywordKey", ArabicText.Normalize(rule.Keyword)),
                ("@supplier", rule.SupplierName),
                ("@supplierKey", NormalizeName(rule.SupplierName)),
                ("@unit", rule.Unit),
                ("@priority", rule.Priority));

            UpsertSavedSupplier(connection, transaction, rule.SupplierName);
        }

        transaction.Commit();
    }

    public List<PurchaseItem> GetPurchaseList(string supplierName, DateTime date)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = PurchaseRowsSql + " AND o.order_date = @orderDate ORDER BY oi.product_name, oi.unit, b.name;";
        command.Parameters.AddWithValue("@supplierKey", NormalizeName(supplierName));
        command.Parameters.AddWithValue("@orderDate", date.ToString(DateFormat));

        return BuildPurchaseItems(ReadPurchaseRows(command));
    }

    public List<MonthlySupplierSummary> GetMonthlySuppliersSummary(string yearMonth)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT oi.supplier_key, MIN(oi.supplier_name) AS supplier_name,
                   COUNT(DISTINCT oi.product_key || '|' || oi.unit_key) AS product_count,
                   SUM(oi.quantity) AS total_quantity
            FROM order_items oi
            INNER JOIN orders o ON o.id = oi.order_id
            WHERE oi.supplier_key <> '' AND strftime('%Y-%m', o.order_date) = @yearMonth
            GROUP BY oi.supplier_key
            ORDER BY supplier_name;
            """;
        command.Parameters.AddWithValue("@yearMonth", yearMonth);

        using var reader = command.ExecuteReader();
        var summaries = new List<MonthlySupplierSummary>();
        while (reader.Read())
        {
            summaries.Add(new MonthlySupplierSummary
            {
                SupplierKey = reader.GetString(0),
                SupplierName = reader.GetString(1),
                ProductCount = reader.GetInt32(2),
                TotalQuantity = reader.GetDouble(3)
            });
        }

        return summaries;
    }

    public List<PurchaseItem> GetMonthlyPurchaseList(string supplierName, string yearMonth)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = PurchaseRowsSql + " AND strftime('%Y-%m', o.order_date) = @yearMonth ORDER BY oi.product_name, oi.unit, b.name;";
        command.Parameters.AddWithValue("@supplierKey", NormalizeName(supplierName));
        command.Parameters.AddWithValue("@yearMonth", yearMonth);

        return BuildPurchaseItems(ReadPurchaseRows(command));
    }

    private const string PurchaseRowsSql = """
        SELECT oi.product_name, oi.product_key, oi.quantity, oi.unit, oi.unit_key, oi.notes, b.name
        FROM order_items oi
        INNER JOIN orders o ON o.id = oi.order_id
        INNER JOIN branches b ON b.id = o.branch_id
        WHERE oi.supplier_key = @supplierKey
        """;

    private static List<RawPurchaseRow> ReadPurchaseRows(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var rows = new List<RawPurchaseRow>();
        while (reader.Read())
        {
            rows.Add(new RawPurchaseRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetDouble(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6)));
        }

        return rows;
    }

    /// <summary>يجمع الكميات لكل (منتج + وحدة) ويفصّل مصدر كل كمية حسب الفرع.</summary>
    private static List<PurchaseItem> BuildPurchaseItems(List<RawPurchaseRow> rows)
    {
        var rowNumber = 1;
        return rows
            .GroupBy(row => new { row.ProductKey, row.UnitKey })
            .OrderBy(group => group.First().ProductName)
            .ThenBy(group => group.First().Unit)
            .Select(group =>
            {
                var branchSources = group
                    .GroupBy(row => NormalizeName(row.BranchName))
                    .Select(branchGroup =>
                        $"{branchGroup.First().BranchName}: {FormatQuantity(branchGroup.Sum(row => row.Quantity))}");

                var notes = group
                    .Select(row => CleanName(row.Notes))
                    .Where(note => note.Length > 0)
                    .Distinct();

                return new PurchaseItem
                {
                    RowNumber = rowNumber++,
                    ProductName = group.First().ProductName,
                    TotalQuantity = group.Sum(row => row.Quantity),
                    Unit = group.First().Unit,
                    Sources = string.Join("، ", branchSources),
                    Notes = string.Join("، ", notes)
                };
            })
            .ToList();
    }

    public static string CleanName(string? value)
    {
        return Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
    }

    public static string NormalizeName(string? value)
    {
        return CleanName(value).ToUpperInvariant();
    }

    private static string FormatQuantity(double quantity)
    {
        return quantity % 1 == 0 ? quantity.ToString("0", CultureInfo.InvariantCulture) : quantity.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();

        return connection;
    }

    private void CreateDailyBackup()
    {
        var backupDirectory = Path.Combine(Path.GetDirectoryName(_dbPath)!, "backups");
        Directory.CreateDirectory(backupDirectory);

        var backupPath = Path.Combine(backupDirectory, $"supplier_purchases_{DateTime.Today:yyyyMMdd}.db");
        if (!File.Exists(backupPath))
        {
            File.Copy(_dbPath, backupPath);
        }

        try
        {
            var directory = new DirectoryInfo(backupDirectory);
            var oldFiles = directory.GetFiles("supplier_purchases_*.db")
                                    .OrderByDescending(f => f.CreationTime)
                                    .Skip(30);
            foreach (var file in oldFiles)
            {
                file.Delete();
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private static int UpsertBranch(SqliteConnection connection, SqliteTransaction transaction, string branchName)
    {
        var key = NormalizeName(branchName);
        var existingId = FindIdByKey(connection, transaction, "branches", "normalized_name", key);
        if (existingId.HasValue)
        {
            using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = "UPDATE branches SET name = @name WHERE id = @id;";
            updateCommand.Parameters.AddWithValue("@name", branchName);
            updateCommand.Parameters.AddWithValue("@id", existingId.Value);
            updateCommand.ExecuteNonQuery();
            return existingId.Value;
        }

        using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = "INSERT INTO branches (name, normalized_name) VALUES (@name, @key);";
        insertCommand.Parameters.AddWithValue("@name", branchName);
        insertCommand.Parameters.AddWithValue("@key", key);
        insertCommand.ExecuteNonQuery();
        return GetLastInsertId(connection, transaction);
    }

    private static int InsertOrder(SqliteConnection connection, SqliteTransaction transaction, int branchId, DateTime orderDate, string notes)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO orders (branch_id, order_date, notes, created_at, updated_at)
            VALUES (@branchId, @orderDate, @notes, @now, @now);
            """;
        command.Parameters.AddWithValue("@branchId", branchId);
        command.Parameters.AddWithValue("@orderDate", orderDate.ToString(DateFormat));
        command.Parameters.AddWithValue("@notes", CleanName(notes));
        command.Parameters.AddWithValue("@now", DateTime.Now.ToString("s"));
        command.ExecuteNonQuery();
        return GetLastInsertId(connection, transaction);
    }

    private static void UpdateOrder(SqliteConnection connection, SqliteTransaction transaction, int orderId, int branchId, DateTime orderDate, string notes)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE orders
            SET branch_id = @branchId, order_date = @orderDate, notes = @notes, updated_at = @now
            WHERE id = @id;
            """;
        command.Parameters.AddWithValue("@branchId", branchId);
        command.Parameters.AddWithValue("@orderDate", orderDate.ToString(DateFormat));
        command.Parameters.AddWithValue("@notes", CleanName(notes));
        command.Parameters.AddWithValue("@now", DateTime.Now.ToString("s"));
        command.Parameters.AddWithValue("@id", orderId);
        command.ExecuteNonQuery();
    }

    private static void DeleteOrderItems(SqliteConnection connection, SqliteTransaction transaction, int orderId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM order_items WHERE order_id = @orderId;";
        command.Parameters.AddWithValue("@orderId", orderId);
        command.ExecuteNonQuery();
    }

    private static void InsertOrderItem(SqliteConnection connection, SqliteTransaction transaction, int orderId, CleanOrderItem item)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO order_items
                (order_id, product_name, product_key, quantity, unit, unit_key, supplier_name, supplier_key, notes)
            VALUES
                (@orderId, @productName, @productKey, @quantity, @unit, @unitKey, @supplierName, @supplierKey, @notes);
            """;
        command.Parameters.AddWithValue("@orderId", orderId);
        command.Parameters.AddWithValue("@productName", item.ProductName);
        command.Parameters.AddWithValue("@productKey", NormalizeName(item.ProductName));
        command.Parameters.AddWithValue("@quantity", item.Quantity);
        command.Parameters.AddWithValue("@unit", item.Unit);
        command.Parameters.AddWithValue("@unitKey", NormalizeName(item.Unit));
        command.Parameters.AddWithValue("@supplierName", item.SupplierName);
        command.Parameters.AddWithValue("@supplierKey", NormalizeName(item.SupplierName));
        command.Parameters.AddWithValue("@notes", item.Notes);
        command.ExecuteNonQuery();
    }

    private static void UpsertSavedProduct(SqliteConnection connection, SqliteTransaction transaction, string productName, string unit)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO saved_products (name, normalized_name, default_unit, last_used_at)
            VALUES (@name, @key, @unit, @now)
            ON CONFLICT(normalized_name) DO UPDATE SET
                name = excluded.name,
                default_unit = excluded.default_unit,
                last_used_at = excluded.last_used_at;
            """;
        command.Parameters.AddWithValue("@name", productName);
        command.Parameters.AddWithValue("@key", NormalizeName(productName));
        command.Parameters.AddWithValue("@unit", unit);
        command.Parameters.AddWithValue("@now", DateTime.Now.ToString("s"));
        command.ExecuteNonQuery();
    }

    private static void UpsertSavedSupplier(SqliteConnection connection, SqliteTransaction transaction, string supplierName)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO saved_suppliers (name, normalized_name, last_used_at)
            VALUES (@name, @key, @now)
            ON CONFLICT(normalized_name) DO UPDATE SET
                name = excluded.name,
                last_used_at = excluded.last_used_at;
            """;
        command.Parameters.AddWithValue("@name", supplierName);
        command.Parameters.AddWithValue("@key", NormalizeName(supplierName));
        command.Parameters.AddWithValue("@now", DateTime.Now.ToString("s"));
        command.ExecuteNonQuery();
    }

    private static void Execute(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    private static bool ColumnExists(SqliteConnection connection, string table, string column)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = @column;";
        command.Parameters.AddWithValue("@column", column);
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    private static string? ReadMetaValue(SqliteConnection connection, string key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM app_meta WHERE key = @key;";
        command.Parameters.AddWithValue("@key", key);
        return command.ExecuteScalar() as string;
    }

    private static void WriteMetaValue(SqliteConnection connection, string key, string value)
    {
        Execute(connection, null, """
            INSERT INTO app_meta (key, value) VALUES (@key, @value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """,
            ("@key", key), ("@value", value));
    }

    private static int? FindIdByKey(SqliteConnection connection, SqliteTransaction transaction, string table, string column, string key)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT id FROM {table} WHERE {column} = @key LIMIT 1;";
        command.Parameters.AddWithValue("@key", key);
        var value = command.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static int GetLastInsertId(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT last_insert_rowid();";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static int ExecuteScalarInt(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static DateTime ParseDate(string value)
    {
        return DateTime.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : DateTime.Today;
    }

    private sealed record CleanOrderItem(string ProductName, double Quantity, string Unit, string SupplierName, string Notes);

    private sealed record RawPurchaseRow(
        string ProductName,
        string ProductKey,
        double Quantity,
        string Unit,
        string UnitKey,
        string Notes,
        string BranchName);
}
