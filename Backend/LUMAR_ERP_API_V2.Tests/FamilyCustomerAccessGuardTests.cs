using System.Data;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Customers;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class FamilyCustomerAccessGuardTests
{
    [Fact]
    public void IsAllowed_AllowsPrimaryAndManagedCustomerIds()
    {
        var user = new CurrentUserDto(
            1,
            "family_user",
            "Family User",
            "Customer",
            true,
            null,
            "Customer",
            10,
            null,
            null)
        {
            ManagedCustomerIds = new[] { 20, 30 }
        };

        Assert.True(CustomerOrderOwnershipGuard.IsAllowed(user, 10));
        Assert.True(CustomerOrderOwnershipGuard.IsAllowed(user, 20));
        Assert.False(CustomerOrderOwnershipGuard.IsAllowed(user, 99));
    }

    [Fact]
    public async Task CreateManagedCustomerAsync_CreatesManagedAccessRow_WithoutTransactionCompletionError()
    {
        var connectionString = GetValidationConnectionString();
        var repository = CreateRepository(connectionString);
        var username = "ياسين الفقيه";
        var customerName = $"فرد عائلة {Guid.NewGuid():N}";
        var phoneNumber = $"966{Random.Shared.Next(100000000, 999999999)}";

        try
        {
            var created = await repository.CreateManagedCustomerAsync(username, new CreateManagedCustomerDto
            {
                CustomerName = customerName,
                PhoneNumber = phoneNumber,
                RelationshipType = "زوجة",
                Address = "اختبار عائلة",
                Notes = "اختبار تلقائي لحقل العائلة"
            }, CancellationToken.None);

            Assert.NotEqual(0, created.CustomerId);
            Assert.Equal("Managed", created.AccessRole);
            Assert.Equal(customerName, created.CustomerName);
            Assert.Equal(1, await CountAccessAsync(connectionString, username, created.CustomerId));
        }
        finally
        {
            await CleanupManagedCustomerAsync(connectionString, customerName);
        }
    }

    [Fact]
    public async Task CreateManagedCustomerAsync_Reuses_Existing_Customer_When_Name_And_Phone_Match()
    {
        var connectionString = GetValidationConnectionString();
        var repository = CreateRepository(connectionString);
        var username = "ياسين الفقيه";
        var customerName = $"مستخدم متكرر {Guid.NewGuid():N}";
        var phoneNumber = $"967{Random.Shared.Next(700000000, 799999999)}";

        try
        {
            var first = await repository.CreateManagedCustomerAsync(username, new CreateManagedCustomerDto
            {
                CustomerName = customerName,
                PhoneNumber = phoneNumber,
                RelationshipType = "أخ",
                Address = "اختبار إعادة الاستخدام"
            }, CancellationToken.None);

            var second = await repository.CreateManagedCustomerAsync(username, new CreateManagedCustomerDto
            {
                CustomerName = customerName,
                PhoneNumber = phoneNumber,
                RelationshipType = "أخ",
                Address = "اختبار إعادة الاستخدام"
            }, CancellationToken.None);

            Assert.Equal(first.CustomerId, second.CustomerId);
            Assert.Equal(first.CustomerCode, second.CustomerCode);
        }
        finally
        {
            await CleanupManagedCustomerAsync(connectionString, customerName);
        }
    }

    private static CustomerRepository CreateRepository(string connectionString)
    {
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        return new CustomerRepository(new ReadOnlySqlConnectionFactory(options), new OperationalSqlConnectionFactory(options));
    }

    private static string GetValidationConnectionString()
    {
        var value = Environment.GetEnvironmentVariable("Lumar__ConnectionString")
            ?? throw new InvalidOperationException("Set Lumar__ConnectionString to the approved validation database.");
        var builder = new SqlConnectionStringBuilder(value);
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Family customer tests require LUMAR_ERP_ES_VALIDATION.");
        return builder.ConnectionString;
    }

    private static async Task<int> CountAccessAsync(string connectionString, string username, int customerId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            SELECT COUNT(*)
            FROM dbo.MobileAccountCustomerAccess mac
            JOIN dbo.MobileAccounts ma ON ma.MobileAccountId = mac.MobileAccountId
            WHERE ma.Username = @username
              AND ma.IsActive = 1
              AND mac.CustomerId = @customerId
              AND mac.AccessRole = N'Managed'
              AND mac.IsActive = 1;", connection);
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@customerId", customerId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task CleanupManagedCustomerAsync(string connectionString, string customerName)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(@"
            DECLARE @customerId int = (
                SELECT TOP (1) CustomerID
                FROM dbo.Customers
                WHERE CustomerName = @customerName
                ORDER BY CustomerID DESC
            );

            IF @customerId IS NOT NULL
            BEGIN
                DELETE FROM dbo.MobileAccountCustomerAccess WHERE CustomerId = @customerId;
                DELETE FROM dbo.Customers WHERE CustomerID = @customerId;
            END;", connection);
        command.Parameters.AddWithValue("@customerName", customerName);
        await command.ExecuteNonQueryAsync();
    }
}
