using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class SupplierCreationIntegrationTests
{
    [Fact]
    public async Task Create_rejects_duplicate_code_and_cleans_up()
    {
        var connectionString = GetConnectionString();
        var code = $"ES7-S-{Guid.NewGuid():N}"[..30];
        var repository = CreateRepository(connectionString);
        try
        {
            var created = await repository.CreateAsync(Request(code), CancellationToken.None);

            Assert.True(created.SupplierId > 0);
            Assert.Equal(code, created.SupplierCode);
            Assert.Equal(1, await CountAsync(connectionString, code));
            await Assert.ThrowsAsync<InvalidOperationException>(() => repository.CreateAsync(Request(code), CancellationToken.None));
            Assert.Equal(1, await CountAsync(connectionString, code));
        }
        finally
        {
            await DeleteAsync(connectionString, code);
            Assert.Equal(0, await CountAsync(connectionString, code));
        }
    }

    [Fact]
    public async Task Concurrent_create_with_same_code_creates_one_supplier()
    {
        var connectionString = GetConnectionString();
        var code = $"ES7-S-{Guid.NewGuid():N}"[..30];
        try
        {
            async Task<bool> AttemptAsync()
            {
                try
                {
                    await CreateRepository(connectionString).CreateAsync(Request(code), CancellationToken.None);
                    return true;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }

            var results = await Task.WhenAll(AttemptAsync(), AttemptAsync());

            Assert.Single(results, result => result);
            Assert.Equal(1, await CountAsync(connectionString, code));
        }
        finally
        {
            await DeleteAsync(connectionString, code);
            Assert.Equal(0, await CountAsync(connectionString, code));
        }
    }

    private static CreateSupplierRequestDto Request(string code) => new()
    {
        SupplierCode = code,
        SupplierName = "مورد اختبار ES7",
        Phone = "777000000",
        Email = "es7-test@example.invalid",
        Address = "بيانات اختبار مؤقتة",
        SourceOperationId = Guid.NewGuid()
    };

    private static SupplierRepository CreateRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new SupplierRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
    }

    private static string GetConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Set Lumar__ConnectionString to the approved validation database.");
        var builder = new SqlConnectionStringBuilder(value);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Supplier creation tests require LUMAR_ERP_ES_VALIDATION.");
        return builder.ConnectionString;
    }

    private static async Task<int> CountAsync(string connectionString, string code)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT COUNT(*) FROM dbo.Suppliers WHERE SupplierCode=@code", connection);
        command.Parameters.AddWithValue("@code", code);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task DeleteAsync(string connectionString, string code)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("DELETE FROM dbo.Suppliers WHERE SupplierCode=@code", connection);
        command.Parameters.AddWithValue("@code", code);
        await command.ExecuteNonQueryAsync();
    }
}