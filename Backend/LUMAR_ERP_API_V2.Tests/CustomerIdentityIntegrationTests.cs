using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Controllers;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Customers;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class CustomerIdentityIntegrationTests
{
    private readonly string connectionString;
    private readonly CustomerRepository customers;
    private readonly AuthRepository auth;
    private readonly CustomersController controller;
    private static readonly CancellationToken Token = CancellationToken.None;

    public CustomerIdentityIntegrationTests()
    {
        var configured = Environment.GetEnvironmentVariable("LUMAR_CUSTOMER_IDENTITY_TEST_CONNECTION")
            ?? throw new InvalidOperationException("Set the dedicated isolated customer identity test connection.");
        var builder = new SqlConnectionStringBuilder(configured);
        if (builder.InitialCatalog != "LUMAR_CUSTOMER_IDENTITY_TEST_20261101")
            throw new InvalidOperationException("Identity tests refuse validation, historical and unapproved databases.");
        connectionString = builder.ConnectionString;
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var reads = new ReadOnlySqlConnectionFactory(options);
        var writes = new OperationalSqlConnectionFactory(options);
        customers = new CustomerRepository(reads, writes);
        auth = new AuthRepository(writes, customers);
        controller = new CustomersController(new CustomerService(customers, new LoyaltyRepository(reads, writes)), null!);
    }

    [Fact]
    public async Task AdministrativeApi_GeneratesIdentity_AndEnsuresLoyalty()
    {
        var response = await controller.CreateCustomer(new CreateCustomerDto
        {
            CustomerName = Name("Admin"), PhoneNumber = Phone()
        }, Token);
        var created = Assert.IsType<CustomerDetailsDto>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);
        await AssertIdentityAsync(created.CustomerId, created.CustomerCode);
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.LoyaltyAccounts WHERE CustomerId=@id", created.CustomerId));
    }

    [Fact]
    public async Task AdministrativeSuppliedCode_IsValidated_AndCollisionNeverLinks()
    {
        var code = new CustomerIdentityGenerator().GenerateCustomerCode();
        var first = await customers.CreateAsync(new CreateCustomerDto { CustomerCode = code, CustomerName = Name("Manual"), PhoneNumber = Phone() }, Token);
        await AssertIdentityAsync(first.CustomerId, code);
        var collision = await controller.CreateCustomer(new CreateCustomerDto { CustomerCode = code, CustomerName = Name("Collision"), PhoneNumber = Phone() }, Token);
        Assert.IsType<ConflictObjectResult>(collision.Result);
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.Customers WHERE CustomerCode=(SELECT CustomerCode FROM dbo.Customers WHERE CustomerID=@id)", first.CustomerId));
        var invalid = await controller.CreateCustomer(new CreateCustomerDto { CustomerCode = "C12345", CustomerName = Name("Invalid"), PhoneNumber = Phone() }, Token);
        Assert.IsType<BadRequestObjectResult>(invalid.Result);
    }

    [Fact]
    public async Task WindowsReferralEndpoint_PreservesReferralContract()
    {
        var parent = await CreateReferralAsync();
        var response = await controller.CreateCustomerWithReferral(new CreateCustomerWithReferralDto
        {
            CustomerName = Name("Referral"), PhoneNumber = Phone(), ReferrerCustomerId = parent.CustomerId, RelationshipType = "Family"
        }, Token);
        var created = Assert.IsType<CustomerCreationResultDto>(Assert.IsType<CreatedAtActionResult>(response.Result).Value);
        await AssertIdentityAsync(created.CustomerId, created.CustomerCode);
        Assert.Equal(parent.CustomerId, created.ReferrerCustomerId);
        Assert.NotNull(created.RegistrationTransactionId);
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.ReferralAccounts WHERE CustomerId=@id", created.CustomerId));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.ReferralTransactions WHERE ReferredCustomerId=@id", created.CustomerId));
    }

    [Fact]
    public async Task MobileRegistration_AndFamilyCreateReuse_PreserveAllIdentities()
    {
        var username = $"identity_{Guid.NewGuid():N}";
        var phone = Phone();
        var authService = new AuthService(auth);
        var registrationController = new AuthController(authService, NullLogger<AuthController>.Instance);
        var registration = await registrationController.RegisterMobileCustomer(new MobileCustomerRegistrationDto(
            username, "Identity-Test-42!", "Identity-Test-42!", Name("Account"), Name("Primary"), phone), Token);
        var session = Assert.IsType<SessionDto>(Assert.IsType<OkObjectResult>(registration.Result).Value);
        var primaryId = Assert.IsType<int>(session.User.CustomerId);
        var primaryIdentity = await AssertIdentityAsync(primaryId);
        Assert.Null(session.User.GetType().GetProperty("GlobalCustomerUid"));
        Assert.DoesNotContain("GlobalCustomerUid", System.Text.Json.JsonSerializer.Serialize(session), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.Customers WHERE CustomerID=@id", primaryId));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.MobileAccounts WHERE CustomerId=@id", primaryId));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.UserSessions WHERE UserId=@id AND RevokedAtUtc IS NULL", session.User.UserId));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.Customers WHERE GlobalCustomerUid=(SELECT GlobalCustomerUid FROM dbo.Customers WHERE CustomerID=@id)", primaryId));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.MobileAccountCustomerAccess ac JOIN dbo.MobileAccounts ma ON ma.MobileAccountId=ac.MobileAccountId WHERE ma.CustomerId=@id AND ac.CustomerId=@id AND ac.AccessRole=N'Primary' AND ac.IsActive=1 AND ac.RevokedAtUtc IS NULL", primaryId));
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.Authorization = $"Bearer {session.Token}";
        var userContext = new AuthenticatedUserContext(new HttpContextAccessor { HttpContext = httpContext }, authService);
        var linkedController = new CustomersController(new CustomerService(customers, null!), userContext);
        var linkedResponse = await linkedController.GetLinkedCustomersForCurrentMobileAccount(Token);
        var linked = Assert.IsAssignableFrom<IReadOnlyList<LinkedCustomerAccessDto>>(Assert.IsType<OkObjectResult>(linkedResponse.Result).Value);
        var linkedPrimary = Assert.Single(linked);
        Assert.Equal(primaryId, linkedPrimary.CustomerId);
        Assert.Equal(primaryIdentity.Code, linkedPrimary.CustomerCode);
        Assert.Equal("Primary", linkedPrimary.AccessRole);
        Assert.DoesNotContain("GlobalCustomerUid", System.Text.Json.JsonSerializer.Serialize(linkedPrimary), StringComparison.OrdinalIgnoreCase);
        var request = new CreateManagedCustomerDto { CustomerName = Name("Family"), PhoneNumber = phone, RelationshipType = "Family" };
        var createdResponse = await linkedController.CreateManagedCustomerForCurrentMobileAccount(request, Token);
        var first = Assert.IsType<LinkedCustomerAccessDto>(Assert.IsType<CreatedResult>(createdResponse.Result).Value);
        var identity = await AssertIdentityAsync(first.CustomerId, first.CustomerCode);
        Assert.NotEqual(primaryId, first.CustomerId);
        Assert.NotEqual(primaryIdentity.Uid, identity.Uid);
        var reused = await customers.CreateManagedCustomerAsync(username, request, Token);
        Assert.Equal(first.CustomerId, reused.CustomerId);
        Assert.Equal(first.CustomerCode, reused.CustomerCode);
        Assert.Equal(identity, await ReadIdentityAsync(reused.CustomerId));
        var byCode = await customers.CreateManagedCustomerAsync(username, new CreateManagedCustomerDto { CustomerCode = first.CustomerCode, CustomerName = first.CustomerName }, Token);
        Assert.Equal(first.CustomerId, byCode.CustomerId);
        Assert.Equal(identity, await ReadIdentityAsync(byCode.CustomerId));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.MobileAccountCustomerAccess WHERE CustomerId=@id", first.CustomerId));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.MobileAccountCustomerAccess ac JOIN dbo.MobileAccounts ma ON ma.MobileAccountId=ac.MobileAccountId WHERE ma.CustomerId=@id AND ac.AccessRole=N'Primary'", primaryId));
        Assert.Equal(2, (await customers.GetLinkedCustomersAsync(username, Token)).Count);
        Assert.Equal(primaryIdentity, await ReadIdentityAsync(primaryId));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.ReferralAccounts WHERE CustomerId=@id", first.CustomerId));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.ReferralTransactions WHERE ReferredCustomerId=@id", first.CustomerId));
    }

    [Fact]
    public async Task MobileRegistration_ConcurrentRetry_CreatesOnePrimaryAndOneSession()
    {
        var username = $"ic_{Guid.NewGuid():N}";
        var request = new MobileCustomerRegistrationDto(
            username, "Identity-Test-42!", "Identity-Test-42!", Name("Account"), Name("Primary"), Phone());

        async Task<SessionDto?> AttemptAsync()
        {
            try
            {
                return await auth.RegisterMobileCustomerAsync(request, Token);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (SqlException error) when (error.Number is 1205 or 2601 or 2627)
            {
                return null;
            }
        }

        var attempts = await Task.WhenAll(AttemptAsync(), AttemptAsync());
        var session = Assert.IsType<SessionDto>(Assert.Single(attempts.Where(attempt => attempt is not null)));
        var customerId = Assert.IsType<int>(session.User.CustomerId);
        var identity = await AssertIdentityAsync(customerId);

        await Assert.ThrowsAsync<ArgumentException>(() => auth.RegisterMobileCustomerAsync(request, Token));
        Assert.Equal(identity, await ReadIdentityAsync(customerId));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.Customers WHERE CustomerName=(SELECT CustomerName FROM dbo.Customers WHERE CustomerID=@id)", customerId));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.MobileAccounts WHERE CustomerId=@id", customerId));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.UserSessions WHERE UserId=@id AND RevokedAtUtc IS NULL", session.User.UserId));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.MobileAccountCustomerAccess ac JOIN dbo.MobileAccounts ma ON ma.MobileAccountId=ac.MobileAccountId WHERE ma.CustomerId=@id AND ac.AccessRole=N'Primary' AND ac.CustomerId=@id", customerId));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.MobileAccountCustomerAccess ac JOIN dbo.MobileAccounts ma ON ma.MobileAccountId=ac.MobileAccountId WHERE ma.CustomerId=@id AND ac.AccessRole=N'Primary' AND ac.CustomerId<>@id", customerId));
        var linkedPrimary = Assert.Single(await customers.GetLinkedCustomersAsync(username, Token));
        Assert.Equal(customerId, linkedPrimary.CustomerId);
        Assert.Equal(identity.Code, linkedPrimary.CustomerCode);
    }

    [Fact]
    public async Task MobileRegistration_PrimaryInsertFailure_RollsBackEverything()
    {
        var username = $"ir_{Guid.NewGuid():N}";
        var customerName = Name("RollbackPrimary");
        var request = new MobileCustomerRegistrationDto(
            username, "Identity-Test-42!", "Identity-Test-42!", Name("Account"), customerName, Phone());
        var sessionsBefore = await CountAsync("SELECT COUNT(*) FROM dbo.UserSessions", 0);
        var accessBefore = await CountAsync("SELECT COUNT(*) FROM dbo.MobileAccountCustomerAccess", 0);
        var proxy = System.Reflection.DispatchProxy.Create<ICustomerRepository, PrimaryAccessFailureProxy>();
        ((PrimaryAccessFailureProxy)proxy).Repository = customers;
        var isolatedConnection = new SqlConnectionStringBuilder(connectionString) { Pooling = false };
        var options = Options.Create(new DatabaseOptions { ConnectionString = isolatedConnection.ConnectionString });
        var faultedAuth = new AuthRepository(new OperationalSqlConnectionFactory(options), proxy);

        var error = await Assert.ThrowsAsync<SqlException>(() => faultedAuth.RegisterMobileCustomerAsync(request, Token));
        Assert.Equal(545, error.Number);
        Assert.Equal(0, await CountByValueAsync("SELECT COUNT(*) FROM dbo.Customers WHERE CustomerName=@value", customerName));
        Assert.Equal(0, await CountByValueAsync("SELECT COUNT(*) FROM dbo.Users WHERE Username=@value", username));
        Assert.Equal(0, await CountByValueAsync("SELECT COUNT(*) FROM dbo.MobileAccounts WHERE Username=@value", username));
        Assert.Equal(sessionsBefore, await CountAsync("SELECT COUNT(*) FROM dbo.UserSessions", 0));
        Assert.Equal(accessBefore, await CountAsync("SELECT COUNT(*) FROM dbo.MobileAccountCustomerAccess", 0));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.UserSessions s LEFT JOIN dbo.Users u ON u.UserID=s.UserId WHERE u.UserID IS NULL", 0));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.MobileAccountCustomerAccess ac LEFT JOIN dbo.MobileAccounts ma ON ma.MobileAccountId=ac.MobileAccountId LEFT JOIN dbo.Customers c ON c.CustomerID=ac.CustomerId WHERE ma.MobileAccountId IS NULL OR c.CustomerID IS NULL", 0));

        var session = await auth.RegisterMobileCustomerAsync(request, Token);
        var customerId = Assert.IsType<int>(session.User.CustomerId);
        var linkedPrimary = Assert.Single(await customers.GetLinkedCustomersAsync(username, Token));
        Assert.Equal(customerId, linkedPrimary.CustomerId);
        Assert.Equal("Primary", linkedPrimary.AccessRole);
    }

    [Fact]
    public async Task MobileRegistration_HttpEndpoints_ReturnPrimaryAndPreserveFamilyIdentity()
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(AuthController).Assembly.GetName().Name,
            EnvironmentName = "Development"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IAuthService>(new AuthService(auth));
        builder.Services.AddSingleton<ICustomerService>(new CustomerService(customers, null!));
        builder.Services.AddScoped<IAuthenticatedUserContext, AuthenticatedUserContext>();

        await using var application = builder.Build();
        application.MapControllers();
        await application.StartAsync(Token);
        using var client = new HttpClient { BaseAddress = new Uri(Assert.Single(application.Urls)) };
        var username = $"ih_{Guid.NewGuid():N}";
        var phone = Phone();
        var registration = await client.PostAsJsonAsync("/auth/mobile/customer/register", new MobileCustomerRegistrationDto(
            username, "Identity-Test-42!", "Identity-Test-42!", Name("Account"), Name("Primary"), phone), Token);
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var registrationJson = await registration.Content.ReadAsStringAsync(Token);
        Assert.DoesNotContain("GlobalCustomerUid", registrationJson, StringComparison.OrdinalIgnoreCase);
        var session = Assert.IsType<SessionDto>(JsonSerializer.Deserialize<SessionDto>(registrationJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var primaryId = Assert.IsType<int>(session.User.CustomerId);
        var primaryIdentity = await AssertIdentityAsync(primaryId);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.Token);

        var linkedResponse = await client.GetAsync("/mobile/customers/linked", Token);
        Assert.Equal(HttpStatusCode.OK, linkedResponse.StatusCode);
        var linkedJson = await linkedResponse.Content.ReadAsStringAsync(Token);
        Assert.DoesNotContain("GlobalCustomerUid", linkedJson, StringComparison.OrdinalIgnoreCase);
        var linked = JsonSerializer.Deserialize<List<LinkedCustomerAccessDto>>(linkedJson, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var primary = Assert.Single(Assert.IsType<List<LinkedCustomerAccessDto>>(linked));
        Assert.Equal(primaryId, primary.CustomerId);
        Assert.Equal(primaryIdentity.Code, primary.CustomerCode);
        Assert.Equal("Primary", primary.AccessRole);

        var familyRequest = new CreateManagedCustomerDto { CustomerName = Name("Family"), PhoneNumber = phone, RelationshipType = "Family" };
        var familyResponse = await client.PostAsJsonAsync("/mobile/customers/family", familyRequest, Token);
        Assert.Equal(HttpStatusCode.Created, familyResponse.StatusCode);
        var familyJson = await familyResponse.Content.ReadAsStringAsync(Token);
        Assert.DoesNotContain("GlobalCustomerUid", familyJson, StringComparison.OrdinalIgnoreCase);
        var family = Assert.IsType<LinkedCustomerAccessDto>(JsonSerializer.Deserialize<LinkedCustomerAccessDto>(familyJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.NotEqual(primaryId, family.CustomerId);
        var familyIdentity = await AssertIdentityAsync(family.CustomerId, family.CustomerCode);
        var retryResponse = await client.PostAsJsonAsync("/mobile/customers/family", familyRequest, Token);
        Assert.Equal(HttpStatusCode.Created, retryResponse.StatusCode);
        var reused = Assert.IsType<LinkedCustomerAccessDto>(await retryResponse.Content.ReadFromJsonAsync<LinkedCustomerAccessDto>(Token));
        Assert.Equal(family.CustomerId, reused.CustomerId);
        Assert.Equal(family.CustomerCode, reused.CustomerCode);
        Assert.Equal(familyIdentity, await ReadIdentityAsync(reused.CustomerId));
        Assert.Equal(primaryIdentity, await ReadIdentityAsync(primaryId));
        Assert.Equal(1, await CountByValueAsync("SELECT COUNT(*) FROM dbo.Customers WHERE CustomerName=@value", familyRequest.CustomerName));
        Assert.Equal(1, await CountAsync("SELECT COUNT(*) FROM dbo.MobileAccountCustomerAccess ac JOIN dbo.MobileAccounts ma ON ma.MobileAccountId=ac.MobileAccountId WHERE ma.CustomerId=@id AND ac.AccessRole=N'Primary'", primaryId));
    }

    [Fact]
    public async Task ConcurrentCreates_ProduceDistinctPersistedIdentities()
    {
        var created = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => CreateReferralAsync()));
        var identities = await Task.WhenAll(created.Select(customer => AssertIdentityAsync(customer.CustomerId, customer.CustomerCode)));
        Assert.Equal(8, created.Select(customer => customer.CustomerId).Distinct().Count());
        Assert.Equal(8, identities.Select(identity => identity.Uid).Distinct().Count());
        Assert.Equal(8, identities.Select(identity => identity.Code).Distinct().Count());
    }

    [Fact]
    public async Task Update_PreservesIdentity_AllowsOtherFields_AndSearchesBothFormats()
    {
        var created = await CreateReferralAsync();
        var original = await AssertIdentityAsync(created.CustomerId, created.CustomerCode);
        var updatedName = Name("Updated");
        var updatedPhone = Phone();
        var updated = await customers.UpdateAsync(created.CustomerId, new UpdateCustomerDto
        {
            CustomerCode = created.CustomerCode, CustomerName = updatedName, PhoneNumber = updatedPhone, IsActive = true, Address = "Updated address"
        }, Token);
        Assert.Equal(updatedName, updated!.CustomerName);
        Assert.Equal(updatedPhone, updated.PhoneNumber);
        Assert.Equal(original, await ReadIdentityAsync(created.CustomerId));
        var rejected = await controller.UpdateCustomer(created.CustomerId, new UpdateCustomerDto
        {
            CustomerCode = new CustomerIdentityGenerator().GenerateCustomerCode(), CustomerName = updatedName, PhoneNumber = updatedPhone, IsActive = true
        }, Token);
        Assert.IsType<BadRequestObjectResult>(rejected.Result);
        Assert.Equal(original, await ReadIdentityAsync(created.CustomerId));
        Assert.Contains(await customers.SearchAsync(created.CustomerCode, Token), customer => customer.CustomerId == created.CustomerId);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT TOP(1) CustomerID, CustomerCode FROM dbo.Customers WHERE CustomerCode LIKE N'C[0-9]%' ORDER BY CustomerID", connection);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        var oldId = reader.GetInt32(0);
        var oldCode = reader.GetString(1);
        Assert.Contains(await customers.SearchAsync(oldCode, Token), customer => customer.CustomerId == oldId);
    }

    [Theory]
    [InlineData("GlobalCustomerUid")]
    [InlineData("CustomerCode")]
    public async Task SqlUniqueness_RejectsCollision(string column)
    {
        var first = await CreateReferralAsync();
        var second = await CreateReferralAsync();
        var before = await ReadIdentityAsync(second.CustomerId);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        await using var command = new SqlCommand($"UPDATE dbo.Customers SET [{column}]=(SELECT [{column}] FROM dbo.Customers WHERE CustomerID=@first) WHERE CustomerID=@second", connection, transaction);
        command.Parameters.AddWithValue("@first", first.CustomerId);
        command.Parameters.AddWithValue("@second", second.CustomerId);
        var error = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
        Assert.Contains(error.Number, new[] { 2601, 2627 });
        await transaction.RollbackAsync();
        Assert.Equal(before, await ReadIdentityAsync(second.CustomerId));
    }

    [Fact]
    public async Task TransactionRollback_RemovesUncommittedCustomerAndReferralEffects()
    {
        var parent = await CreateReferralAsync();
        var before = await CountAsync("SELECT TotalReferrals FROM dbo.ReferralAccounts WHERE CustomerId=@id", parent.CustomerId);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        var created = await customers.CreateWithReferralInTransactionAsync(connection, transaction, new CreateCustomerWithReferralDto
        {
            CustomerName = Name("Rollback"), PhoneNumber = Phone(), ReferrerCustomerId = parent.CustomerId, RelationshipType = "Family"
        }, Token);
        await transaction.RollbackAsync();
        Assert.Null(await customers.GetByIdAsync(created.CustomerId, Token));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.ReferralAccounts WHERE CustomerId=@id", created.CustomerId));
        Assert.Equal(0, await CountAsync("SELECT COUNT(*) FROM dbo.ReferralTransactions WHERE ReferredCustomerId=@id", created.CustomerId));
        Assert.Equal(before, await CountAsync("SELECT TotalReferrals FROM dbo.ReferralAccounts WHERE CustomerId=@id", parent.CustomerId));
    }

    private Task<CustomerCreationResultDto> CreateReferralAsync() => customers.CreateWithReferralAsync(new CreateCustomerWithReferralDto { CustomerName = Name("Customer"), PhoneNumber = Phone() }, Token);
    private static string Name(string kind) => $"Identity {kind} {Guid.NewGuid():N}";
    private static string Phone() => "77" + System.Security.Cryptography.RandomNumberGenerator.GetInt32(100_000_000, 999_999_999);

    private async Task<(Guid Uid, string Code)> AssertIdentityAsync(int customerId, string? code = null)
    {
        Assert.True(customerId > 0);
        var identity = await ReadIdentityAsync(customerId);
        Assert.Equal(7, identity.Uid.Version);
        Assert.True(new CustomerIdentityGenerator().ValidateNewCustomerCode(identity.Code));
        if (code is not null) Assert.Equal(code, identity.Code);
        return identity;
    }

    private async Task<(Guid Uid, string Code)> ReadIdentityAsync(int customerId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT GlobalCustomerUid, CustomerCode FROM dbo.Customers WHERE CustomerID=@id", connection);
        command.Parameters.AddWithValue("@id", customerId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetGuid(0), reader.GetString(1));
    }

    private async Task<int> CountAsync(string sql, int customerId)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@id", customerId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> CountByValueAsync(string sql, string value)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(Token);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@value", value);
        return Convert.ToInt32(await command.ExecuteScalarAsync(Token));
    }

    public class PrimaryAccessFailureProxy : System.Reflection.DispatchProxy
    {
        public ICustomerRepository Repository { get; set; } = null!;

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name != nameof(ICustomerRepository.CreateWithReferralInTransactionAsync) || args is null)
                throw new NotSupportedException();

            return CreateWithBlockedPrimaryAsync(
                (SqlConnection)args[0]!,
                (SqlTransaction)args[1]!,
                (CreateCustomerWithReferralDto)args[2]!,
                (CancellationToken)args[3]!);
        }

        private async Task<CustomerCreationResultDto> CreateWithBlockedPrimaryAsync(
            SqlConnection connection, SqlTransaction transaction, CreateCustomerWithReferralDto request, CancellationToken cancellationToken)
        {
            var result = await Repository.CreateWithReferralInTransactionAsync(connection, transaction, request, cancellationToken);
            await using var command = new SqlCommand("SET IDENTITY_INSERT dbo.MobileAccountCustomerAccess ON", connection, transaction);
            await command.ExecuteNonQueryAsync(cancellationToken);
            return result;
        }
    }
}