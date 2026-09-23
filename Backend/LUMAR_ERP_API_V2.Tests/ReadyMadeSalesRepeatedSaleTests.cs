using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class ReadyMadeSalesRepeatedSaleTests
{
    [Fact]
    public async Task CreateAsync_CompletesTwoSequentialSalesWithRealTrackingReferences()
    {
        var connectionString = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? "Data Source=YASIN-YASIN\\SQLEXPRESS;Initial Catalog=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var repository = new ReadyMadeSalesRepository(
            new ReadOnlySqlConnectionFactory(options),
            new OperationalSqlConnectionFactory(options));
        var seed = await ReadSeedAsync(connectionString);
        var firstReference = $"RMS-REPEAT-{Guid.NewGuid():N}-1";
        var secondReference = $"RMS-REPEAT-{Guid.NewGuid():N}-2";

        var first = await repository.CreateAsync(BuildSale(seed, firstReference), CancellationToken.None);
        var second = await repository.CreateAsync(BuildSale(seed, secondReference), CancellationToken.None);

        Assert.NotEqual(first.InvoiceId, second.InvoiceId);
        Assert.Single(first.Items);
        Assert.Single(second.Items);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            SELECT COUNT(*), COUNT(DISTINCT d.TrackingCode), COUNT(pt.TrackingCode)
            FROM dbo.Invoice_Details d
            INNER JOIN dbo.Production_Tracking pt ON pt.TrackingCode = d.TrackingCode
            WHERE d.InvoiceID IN (@firstInvoiceId, @secondInvoiceId);", connection);
        command.Parameters.AddWithValue("@firstInvoiceId", first.InvoiceId);
        command.Parameters.AddWithValue("@secondInvoiceId", second.InvoiceId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt32(0));
        Assert.Equal(2, reader.GetInt32(1));
        Assert.Equal(2, reader.GetInt32(2));
    }

    private static CreateReadyMadeSaleDto BuildSale((int CustomerId, int ProductId) seed, string reference) => new()
    {
        CustomerId = seed.CustomerId,
        PaymentType = "Cash",
        PaidAmount = 1m,
        SaleReference = reference,
        Items = [new CreateReadyMadeSaleItemDto
        {
            ImportedReadyMadeProductId = seed.ProductId,
            Quantity = 1,
            UnitPrice = 1m,
        }],
    };

    private static async Task<(int CustomerId, int ProductId)> ReadSeedAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            SELECT TOP (1) c.CustomerID, p.ImportedReadyMadeProductId
            FROM dbo.Customers c
            CROSS JOIN dbo.ImportedReadyMadeProducts p
            WHERE c.IsActive = 1 AND p.IsActive = 1 AND p.Quantity >= 2
            ORDER BY c.CustomerID, p.ImportedReadyMadeProductId;", connection);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) throw new InvalidOperationException("لا توجد بيانات اختبار متاحة لبيعين متتاليين.");
        return (reader.GetInt32(0), reader.GetInt32(1));
    }
}