using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class ReadyMadeSalesRepeatedSaleTests
{
    [Fact]
    public async Task UpsertImportedProductAsync_GeneratesCanonicalProductCodeWhenBlank()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var repository = new InventoryRepository(
            new ReadOnlySqlConnectionFactory(options),
            new OperationalSqlConnectionFactory(options));

        var expectedPrefix = await ReadConfiguredImportedProductPrefixAsync(connectionString);

        var product = await repository.UpsertImportedProductAsync(new CreateImportedProductDto
        {
            ProductName = $"Auto Code Test {Guid.NewGuid():N}",
            ProductType = "جاهز",
            ProductCode = string.Empty,
            Unit = "قطعة",
            Quantity = 1m,
            PurchasePrice = 10m,
            SellingPrice = 20m,
            Category = "اختبار",
            Notes = "Test generated product code"
        }, CancellationToken.None);

        Assert.NotNull(product);
        Assert.False(string.IsNullOrWhiteSpace(product!.ProductCode));
        Assert.StartsWith(expectedPrefix, product.ProductCode);
    }

    [Fact]
    public async Task CreateAsync_RejectsSeededImportedProductWithoutOfficialReceipt()
    {
        var connectionString = GetValidationConnectionString();
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var repository = new ReadyMadeSalesRepository(
            new ReadOnlySqlConnectionFactory(options),
            new OperationalSqlConnectionFactory(options));
        var seed = await SeedAsync(connectionString);
        var reference = $"RMS-REPEAT-{Guid.NewGuid():N}";

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(BuildSale(seed, reference), CancellationToken.None));
            Assert.Equal("لا يوجد رصيد مخزون رسمي للمنتج المستورد؛ لن يتم ربط سجل تاريخي تلقائياً.", exception.Message);
        }
        finally
        {
            await CleanupAsync(connectionString, seed);
        }
    }

    private static async Task<string> ReadConfiguredImportedProductPrefixAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            SELECT TOP (1) SettingValue
            FROM dbo.System_Settings WITH (NOLOCK)
            WHERE SettingName IN (N'ImportedProductCodePrefix', N'ImportedReadyMadeProductCodePrefix')
            ORDER BY CASE WHEN SettingName = N'ImportedProductCodePrefix' THEN 0 ELSE 1 END;", connection);
        var value = await command.ExecuteScalarAsync();
        var prefix = value is null || value is DBNull ? "AB-" : value.ToString();
        return string.IsNullOrWhiteSpace(prefix) ? "AB-" : prefix.Trim();
    }

    private static CreateReadyMadeSaleDto BuildSale((int CustomerId, int ProductId) seed, string reference) => new()
    {
        CustomerId = seed.CustomerId,
        PaymentType = "Cash",
        CashAccountId = 1,
        PaidAmount = 1m,
        SaleReference = reference,
        Items = [new CreateReadyMadeSaleItemDto
        {
            ImportedReadyMadeProductId = seed.ProductId,
            Quantity = 1,
            UnitPrice = 1m,
        }],
    };

    private static string GetValidationConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Lumar__ConnectionString must target the validation database.");
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_CUSTOMERS_ONLY_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ready-made repeated-sale tests are restricted to the validation database.");
        return builder.ConnectionString;
    }

    private static async Task<(int CustomerId, int ProductId)> SeedAsync(string connectionString)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            await using var command = new SqlCommand(@"
                INSERT INTO dbo.Customers (CustomerCode, CustomerName, PhoneNumber, TotalPoints, TotalPieces, TotalDebts, IsActive)
                OUTPUT INSERTED.CustomerID
                VALUES (@customerCode, N'عميل اختبار بيع جاهز', @phoneNumber, 0, 0, 0, 1);", connection, transaction);
            command.Parameters.AddWithValue("@customerCode", $"RMS-{suffix}");
            command.Parameters.AddWithValue("@phoneNumber", $"011{suffix[..9]}");
            var customerId = Convert.ToInt32(await command.ExecuteScalarAsync());

            command.CommandText = @"
                INSERT INTO dbo.ImportedReadyMadeProducts
                    (ProductName, ProductType, ProductCode, Unit, Quantity, PurchasePrice, SellingPrice, IsActive, Category, CreatedAt)
                OUTPUT INSERTED.ImportedReadyMadeProductId
                VALUES (N'منتج مستورد لاختبار الرفض', N'جاهز', @productCode, N'قطعة', 2, 10, 20, 1, N'اختبار', SYSUTCDATETIME());";
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@productCode", $"RMS-{suffix}");
            var productId = Convert.ToInt32(await command.ExecuteScalarAsync());
            await transaction.CommitAsync();
            return (customerId, productId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task CleanupAsync(string connectionString, (int CustomerId, int ProductId) seed)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            DELETE FROM dbo.ImportedReadyMadeProducts WHERE ImportedReadyMadeProductId = @productId;
            DELETE FROM dbo.Customers WHERE CustomerID = @customerId;", connection);
        command.Parameters.AddWithValue("@productId", seed.ProductId);
        command.Parameters.AddWithValue("@customerId", seed.CustomerId);
        await command.ExecuteNonQueryAsync();
    }
}