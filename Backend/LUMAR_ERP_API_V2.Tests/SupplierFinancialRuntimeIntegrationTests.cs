using System.Data;
using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Auth;
using LUMAR_ERP_API_V2.DTOs.Suppliers;
using LUMAR_ERP_API_V2.FinancialFoundation;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class SupplierFinancialRuntimeIntegrationTests
{
    [Fact]
    public async Task InvoiceWorkflow_CreatesReadsRetriesAndReversesMultipleLinesAtomically()
    {
        var fixture = await CreateCommittedFixtureAsync();
        var itemIds = new List<int>();
        try
        {
            await using (var connection = new SqlConnection(ConnectionString()))
            {
                await connection.OpenAsync();
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
                for (var index = 1; index <= 2; index++)
                {
                    await using var item = new SqlCommand("INSERT dbo.InventoryItems(ItemCode,ItemName,Category,Unit,CurrentQuantity,AvailableQuantity,ReservedQuantity,IsActive,CreatedAt,UpdatedAt) OUTPUT INSERTED.InventoryItemID VALUES(@code,@name,N'Foundation',N'Piece',0,0,0,1,SYSUTCDATETIME(),SYSUTCDATETIME());", connection, transaction);
                    item.Parameters.AddWithValue("@code", $"ES7-I-{Guid.NewGuid():N}"[..18]);
                    item.Parameters.AddWithValue("@name", $"ES7 invoice item {index}");
                    itemIds.Add(Convert.ToInt32(await item.ExecuteScalarAsync()));
                }
                await transaction.CommitAsync();
            }

            var options = Options.Create(new DatabaseOptions { ConnectionString = ConnectionString() });
            var coordinator = new SupplierFinancialWorkflowCoordinator(
                new OperationalSqlConnectionFactory(options),
                new SupplierFinancialRuntime(),
                new Es7OperationalAudit(NullLogger<Es7OperationalAudit>.Instance));
            var operation = Guid.NewGuid();
            var request = new CreateSupplierInvoiceRequestDto(
                fixture.SupplierId, $"ES7-LINES-{Guid.NewGuid():N}", new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 30), 44m, null, operation,
                Lines: [new(itemIds[0], 2m, 10m), new(itemIds[1], 2m, 12m)]);
            var user = new CurrentUserDto(7, "es7-lines-test", "ES7 Lines Test", "System Administrator", true, null);

            var created = await coordinator.CreateInvoiceAsync(request, user, "es7-lines-create", CancellationToken.None);
            var retry = await coordinator.CreateInvoiceAsync(request, user, "es7-lines-retry", CancellationToken.None);
            var repository = new SupplierInvoiceLineRepository(new ReadOnlySqlConnectionFactory(options));
            var lines = (await repository.GetForInvoiceAsync(created.SupplierInvoiceId, CancellationToken.None))!;

            Assert.True(retry.IsExisting);
            Assert.Equal(created.SupplierInvoiceId, retry.SupplierInvoiceId);
            Assert.Equal(2, lines.Count);
            Assert.Equal(44m, lines.Sum(line => line.LineTotal));
            await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.CreateInvoiceAsync(
                request with { Lines = [new(itemIds[0], 1m, 20m), new(itemIds[1], 2m, 12m)] }, user, "es7-lines-conflict", CancellationToken.None));

            await coordinator.ReverseAsync(new ReverseSupplierFinancialRequestDto("Invoice", created.SupplierInvoiceId, Guid.NewGuid(), "Integration verification"), user, "es7-lines-reverse", CancellationToken.None);
            Assert.All((await repository.GetForInvoiceAsync(created.SupplierInvoiceId, CancellationToken.None))!, line => Assert.Equal("Reversed", line.Status));
        }
        finally
        {
            await CleanupCommittedFixtureAsync(fixture);
            if (itemIds.Count > 0)
            {
                await using var connection = new SqlConnection(ConnectionString());
                await connection.OpenAsync();
                await using var command = new SqlCommand("DELETE FROM dbo.InventoryItems WHERE InventoryItemID IN (SELECT value FROM OPENJSON(@ids));", connection);
                command.Parameters.AddWithValue("@ids", System.Text.Json.JsonSerializer.Serialize(itemIds));
                await command.ExecuteNonQueryAsync();
            }
        }
    }

    [Fact]
    public async Task Allocations_ConcurrentIndependentTransactions_AllowOnlyOneAllocationForTheSamePaymentAndInvoice()
    {
        var fixture = await CreateCommittedFixtureAsync();
        try
        {
            var (invoice, payment) = await CreateInvoiceAndAdvanceAsync(fixture, 100m);
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = AttemptAllocationAsync(payment.SupplierPaymentId, invoice.SupplierInvoiceId, Guid.NewGuid(), "ES5G-ALLOC-1", firstReady, start.Task);
            var second = AttemptAllocationAsync(payment.SupplierPaymentId, invoice.SupplierInvoiceId, Guid.NewGuid(), "ES5G-ALLOC-2", secondReady, start.Task);
            await Task.WhenAll(firstReady.Task, secondReady.Task);
            start.SetResult();

            var results = await Task.WhenAll(first, second);
            Assert.Equal(1, results.Count(result => result));
            Assert.Equal(1, await CountForSupplierAsync("SELECT COUNT(*) FROM dbo.SupplierPaymentAllocations a JOIN dbo.SupplierPayments p ON p.SupplierPaymentId=a.SupplierPaymentId WHERE p.SupplierId=@supplierId", fixture.SupplierId));
            Assert.Equal(100m, await ScalarForInvoiceAsync("SELECT AmountPaid FROM dbo.SupplierInvoices WHERE SupplierInvoiceId=@invoiceId", invoice.SupplierInvoiceId));
        }
        finally { await CleanupCommittedFixtureAsync(fixture); }
    }

    [Fact]
    public async Task InvoiceCreation_ConcurrentIndependentTransactions_AppliesOneAdvanceOnlyOnce()
    {
        var fixture = await CreateCommittedFixtureAsync();
        try
        {
            await CreateAdvanceAsync(fixture, 100m, "ES5G-AUTO-ADV");
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var firstReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var secondReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var first = CreateConcurrentInvoiceAsync(fixture, "ES5G-AUTO-INV-1", firstReady, start.Task);
            var second = CreateConcurrentInvoiceAsync(fixture, "ES5G-AUTO-INV-2", secondReady, start.Task);
            await Task.WhenAll(firstReady.Task, secondReady.Task);
            start.SetResult();

            var invoices = await Task.WhenAll(first, second);
            Assert.Equal([0m, 100m], invoices.Select(result => result.AmountPaid).OrderBy(amount => amount));
            Assert.Equal(100m, await ScalarForSupplierAsync("SELECT COALESCE(SUM(a.AllocatedAmount),0) FROM dbo.SupplierPaymentAllocations a JOIN dbo.SupplierPayments p ON p.SupplierPaymentId=a.SupplierPaymentId WHERE p.SupplierId=@supplierId", fixture.SupplierId));
            Assert.Equal(1, await CountForSupplierAsync("SELECT COUNT(*) FROM dbo.SupplierPaymentAllocations a JOIN dbo.SupplierPayments p ON p.SupplierPaymentId=a.SupplierPaymentId WHERE p.SupplierId=@supplierId", fixture.SupplierId));
        }
        finally { await CleanupCommittedFixtureAsync(fixture); }
    }

    [Fact]
    public async Task AllocationRetry_FromAnIndependentConnection_ReturnsTheOriginalAllocation()
    {
        var fixture = await CreateCommittedFixtureAsync();
        try
        {
            var (invoice, payment) = await CreateInvoiceAndAdvanceAsync(fixture, 75m);
            var operation = Guid.NewGuid();
            var first = await AllocateCommittedAsync(payment.SupplierPaymentId, invoice.SupplierInvoiceId, 75m, operation, "ES5G-RETRY");
            var retry = await AllocateCommittedAsync(payment.SupplierPaymentId, invoice.SupplierInvoiceId, 75m, operation, "ES5G-RETRY");

            Assert.Equal(first.SupplierPaymentAllocationId, retry.SupplierPaymentAllocationId);
            Assert.Equal(first.AccountingEventId, retry.AccountingEventId);
            Assert.True(retry.IsExisting);
            Assert.Equal(1, await CountForSupplierAsync("SELECT COUNT(*) FROM dbo.SupplierFinancialPaymentAllocations f JOIN dbo.SupplierPaymentAllocations a ON a.SupplierPaymentAllocationId=f.SupplierPaymentAllocationId JOIN dbo.SupplierPayments p ON p.SupplierPaymentId=a.SupplierPaymentId WHERE p.SupplierId=@supplierId", fixture.SupplierId));
        }
        finally { await CleanupCommittedFixtureAsync(fixture); }
    }

    [Fact]
    public async Task FinancialRuntime_RollbackIsInvisibleToAnIndependentConnection()
    {
        var fixture = await CreateCommittedFixtureAsync();
        var invoiceOperation = Guid.NewGuid();
        var paymentOperation = Guid.NewGuid();
        try
        {
            await using (var connection = new SqlConnection(ConnectionString()))
            {
                await connection.OpenAsync();
                await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
                var runtime = new SupplierFinancialRuntime();
                var day = new DateOnly(2026, 9, 29);
                var invoice = await runtime.CreateInvoiceAsync(connection, transaction, new SupplierFinancialInvoiceRequest(fixture.SupplierId, "ES5G-ROLLBACK", day, day, 50m, null, invoiceOperation, "es5g-test"), CancellationToken.None);
                await runtime.PayAsync(connection, transaction, new SupplierFinancialPaymentRequest(fixture.SupplierId, invoice.SupplierInvoiceId, 50m, day, fixture.CashAccountId, SupplierPaymentKind.Immediate, "Cash", "ES5G-ROLLBACK-PAY", null, paymentOperation, "es5g-test"), CancellationToken.None);
                await transaction.RollbackAsync();
            }

            await using var verify = new SqlConnection(ConnectionString());
            await verify.OpenAsync();
            foreach (var table in new[] { "dbo.SupplierFinancialInvoices", "dbo.SupplierFinancialPayments", "dbo.SupplierFinancialPaymentAllocations", "dbo.AccountingEvents" })
            {
                await using var command = new SqlCommand($"SELECT COUNT(*) FROM {table} WHERE SourceOperationId IN (@invoiceOperation,@paymentOperation)", verify);
                command.Parameters.Add("@invoiceOperation", SqlDbType.UniqueIdentifier).Value = invoiceOperation;
                command.Parameters.Add("@paymentOperation", SqlDbType.UniqueIdentifier).Value = paymentOperation;
                Assert.Equal(0, Convert.ToInt32(await command.ExecuteScalarAsync()));
            }
        }
        finally { await CleanupCommittedFixtureAsync(fixture); }
    }

    [Fact]
    public async Task Reversals_AllocationAdvancePaymentAndInvoice_AreIdempotentAndPreventDuplicates()
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var supplierId = await InsertSupplierAsync(connection, transaction);
            var cashAccountId = await ConfigureFoundationAsync(connection, transaction);
            var runtime = new SupplierFinancialRuntime();
            var day = new DateOnly(2026, 9, 29);
            var invoice = await runtime.CreateInvoiceAsync(connection, transaction, new SupplierFinancialInvoiceRequest(supplierId, "ES5G-REV-INV", day, day, 100m, null, Guid.NewGuid(), "es5g-test"), CancellationToken.None);
            var advance = await runtime.PayAsync(connection, transaction, new SupplierFinancialPaymentRequest(supplierId, null, 20m, day, cashAccountId, SupplierPaymentKind.Advance, "Cash", "ES5G-REV-ADV", null, Guid.NewGuid(), "es5g-test"), CancellationToken.None);
            var payment = await runtime.PayAsync(connection, transaction, new SupplierFinancialPaymentRequest(supplierId, invoice.SupplierInvoiceId, 80m, day, cashAccountId, SupplierPaymentKind.Later, "Cash", "ES5G-REV-PAY", null, Guid.NewGuid(), "es5g-test"), CancellationToken.None);
            var allocationId = await ScalarIntAsync(connection, transaction, "SELECT SupplierPaymentAllocationId FROM dbo.SupplierPaymentAllocations WHERE SupplierPaymentId=@paymentId", payment.SupplierPaymentId);

            await AssertReversalIsIdempotentAndExclusiveAsync(operation => runtime.ReverseAllocationAsync(connection, transaction, allocationId, operation, "ES5G-REV-ALLOC", "test", "es5g-test", CancellationToken.None));
            await AssertReversalIsIdempotentAndExclusiveAsync(operation => runtime.ReversePaymentAsync(connection, transaction, payment.SupplierPaymentId, operation, "ES5G-REV-PAY", "test", "es5g-test", CancellationToken.None));
            await AssertReversalIsIdempotentAndExclusiveAsync(operation => runtime.ReverseInvoiceAsync(connection, transaction, invoice.SupplierInvoiceId, operation, "ES5G-REV-INV", "test", "es5g-test", CancellationToken.None));
            await AssertReversalIsIdempotentAndExclusiveAsync(operation => runtime.ReversePaymentAsync(connection, transaction, advance.SupplierPaymentId, operation, "ES5G-REV-ADV", "test", "es5g-test", CancellationToken.None));
            Assert.Equal(4, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventType=33", 0));

            await transaction.RollbackAsync();
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    [Fact]
    public async Task FinancialRuntime_PostsSupplierInvoicePaymentsAllocationsAndReversalsAtomically()
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var supplierId = await InsertSupplierAsync(connection, transaction);
            var cashAccountId = await ConfigureFoundationAsync(connection, transaction);
            var runtime = new SupplierFinancialRuntime();
            var day = new DateOnly(2026, 9, 29);

            var advance = await runtime.PayAsync(connection, transaction, new SupplierFinancialPaymentRequest(
                supplierId, null, 30m, day, cashAccountId, SupplierPaymentKind.Advance, "Cash", "SUP-ADV", null, Guid.NewGuid(), "es5-test"), CancellationToken.None);
            Assert.Equal(0m, advance.AmountAllocated);

            var invoice = await runtime.CreateInvoiceAsync(connection, transaction, new SupplierFinancialInvoiceRequest(
                supplierId, "SUP-INV-1", day, day, 100m, null, Guid.NewGuid(), "es5-test"), CancellationToken.None);
            Assert.Equal(30m, invoice.AmountPaid);
            Assert.Equal(70m, invoice.OutstandingAmount);

            var laterRequest = new SupplierFinancialPaymentRequest(
                supplierId, invoice.SupplierInvoiceId, 40m, day, cashAccountId, SupplierPaymentKind.Later, "Cash", "SUP-LATER", null, Guid.NewGuid(), "es5-test");
            var later = await runtime.PayAsync(connection, transaction, laterRequest, CancellationToken.None);
            Assert.Equal(40m, later.AmountAllocated);
            Assert.True((await runtime.PayAsync(connection, transaction, laterRequest, CancellationToken.None)).IsExisting);
            await Assert.ThrowsAsync<InvalidOperationException>(() => runtime.PayAsync(connection, transaction, laterRequest with { Amount = 41m }, CancellationToken.None));

            var immediate = await runtime.CreateInvoiceAsync(connection, transaction, new SupplierFinancialInvoiceRequest(
                supplierId, "SUP-INV-2", day, day, 20m, null, Guid.NewGuid(), "es5-test"), CancellationToken.None);
            var immediatePayment = await runtime.PayAsync(connection, transaction, new SupplierFinancialPaymentRequest(
                supplierId, immediate.SupplierInvoiceId, 20m, day, cashAccountId, SupplierPaymentKind.Immediate, "Cash", "SUP-IMM", null, Guid.NewGuid(), "es5-test"), CancellationToken.None);
            Assert.Equal(20m, immediatePayment.AmountAllocated);

            var laterAllocationId = await ScalarIntAsync(connection, transaction, "SELECT SupplierPaymentAllocationId FROM dbo.SupplierPaymentAllocations WHERE SupplierPaymentId=@paymentId", later.SupplierPaymentId);
            var allocationReversalOperation = Guid.NewGuid();
            var allocationReversal = await runtime.ReverseAllocationAsync(connection, transaction, laterAllocationId, allocationReversalOperation, "SUP-LATER-REV-ALLOC", "test", "es5-test", CancellationToken.None);
            Assert.Equal(allocationReversal, await runtime.ReverseAllocationAsync(connection, transaction, laterAllocationId, allocationReversalOperation, "SUP-LATER-REV-ALLOC", "test", "es5-test", CancellationToken.None));

            var paymentReversalOperation = Guid.NewGuid();
            var paymentReversal = await runtime.ReversePaymentAsync(connection, transaction, later.SupplierPaymentId, paymentReversalOperation, "SUP-LATER-REV", "test", "es5-test", CancellationToken.None);
            Assert.Equal(paymentReversal, await runtime.ReversePaymentAsync(connection, transaction, later.SupplierPaymentId, paymentReversalOperation, "SUP-LATER-REV", "test", "es5-test", CancellationToken.None));

            Assert.Equal(30m, await ScalarDecimalAsync(connection, transaction, "SELECT AmountPaid FROM dbo.SupplierInvoices WHERE SupplierInvoiceId=@invoiceId", invoice.SupplierInvoiceId));
            Assert.Equal(1, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.CashMovements WHERE AccountingEventId=@eventId", advance.AccountingEventId));
            Assert.Equal(1, await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.CashMovements WHERE AccountingEventId=@eventId", immediatePayment.AccountingEventId));
            Assert.Equal(0m, await ScalarDecimalAsync(connection, transaction, "SELECT COALESCE(SUM(DebitAmount-CreditAmount),0) FROM dbo.JournalEntryLines WHERE JournalEntryId=(SELECT JournalEntryId FROM dbo.JournalEntries WHERE AccountingEventId=@eventId)", immediatePayment.AccountingEventId));
            Assert.True(await CountAsync(connection, transaction, "SELECT COUNT(*) FROM dbo.AccountingEvents WHERE AccountingEventType=33", 0) >= 2);

            await transaction.RollbackAsync();
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task AssertReversalIsIdempotentAndExclusiveAsync(Func<Guid, Task<long>> reverse)
    {
        var operation = Guid.NewGuid();
        var reversal = await reverse(operation);
        Assert.Equal(reversal, await reverse(operation));
        await Assert.ThrowsAsync<InvalidOperationException>(() => reverse(Guid.NewGuid()));
    }

    private static async Task<CommittedFixture> CreateCommittedFixtureAsync()
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var mappings = await ReadFoundationStateAsync(connection, transaction);
            var supplierId = await InsertSupplierAsync(connection, transaction);
            var cashAccountId = await ConfigureFoundationAsync(connection, transaction);
            await transaction.CommitAsync();
            return new CommittedFixture(supplierId, cashAccountId, mappings.Mappings, mappings.Events);
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task CleanupCommittedFixtureAsync(CommittedFixture fixture)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await using (var command = new SqlCommand("""
                DECLARE @events TABLE (AccountingEventId bigint NOT NULL PRIMARY KEY, IsReversal bit NOT NULL);
                INSERT @events(AccountingEventId,IsReversal)
                SELECT ae.AccountingEventId,0 FROM dbo.AccountingEvents ae
                WHERE (ae.SourceType=N'SupplierInvoice' AND ae.SourceId IN (SELECT SupplierInvoiceId FROM dbo.SupplierInvoices WHERE SupplierId=@supplierId))
                   OR (ae.SourceType=N'SupplierPayment' AND ae.SourceId IN (SELECT SupplierPaymentId FROM dbo.SupplierPayments WHERE SupplierId=@supplierId))
                   OR (ae.SourceType=N'SupplierPaymentAllocation' AND ae.SourceId IN (SELECT a.SupplierPaymentAllocationId FROM dbo.SupplierPaymentAllocations a JOIN dbo.SupplierPayments p ON p.SupplierPaymentId=a.SupplierPaymentId WHERE p.SupplierId=@supplierId));
                INSERT @events(AccountingEventId,IsReversal)
                SELECT ae.AccountingEventId,1 FROM dbo.AccountingEvents ae JOIN @events original ON original.AccountingEventId=ae.OriginalAccountingEventId
                WHERE NOT EXISTS (SELECT 1 FROM @events existing WHERE existing.AccountingEventId=ae.AccountingEventId);
                DELETE FROM dbo.SupplierFinancialPaymentAllocations WHERE SupplierPaymentAllocationId IN (SELECT a.SupplierPaymentAllocationId FROM dbo.SupplierPaymentAllocations a JOIN dbo.SupplierPayments p ON p.SupplierPaymentId=a.SupplierPaymentId WHERE p.SupplierId=@supplierId);
                DELETE FROM dbo.SupplierFinancialPayments WHERE SupplierPaymentId IN (SELECT SupplierPaymentId FROM dbo.SupplierPayments WHERE SupplierId=@supplierId);
                DELETE FROM dbo.SupplierInvoiceLines WHERE SupplierInvoiceId IN (SELECT SupplierInvoiceId FROM dbo.SupplierInvoices WHERE SupplierId=@supplierId);
                DELETE FROM dbo.SupplierFinancialInvoices WHERE SupplierInvoiceId IN (SELECT SupplierInvoiceId FROM dbo.SupplierInvoices WHERE SupplierId=@supplierId);
                DELETE cm FROM dbo.CashMovements cm JOIN @events e ON e.AccountingEventId=cm.AccountingEventId WHERE e.IsReversal=1;
                DELETE cm FROM dbo.CashMovements cm JOIN @events e ON e.AccountingEventId=cm.AccountingEventId WHERE e.IsReversal=0;
                DELETE jel FROM dbo.JournalEntryLines jel JOIN dbo.JournalEntries je ON je.JournalEntryId=jel.JournalEntryId JOIN @events e ON e.AccountingEventId=je.AccountingEventId;
                DELETE je FROM dbo.JournalEntries je JOIN @events e ON e.AccountingEventId=je.AccountingEventId;
                DELETE ft FROM dbo.FinancialTransactions ft JOIN @events e ON e.AccountingEventId=ft.AccountingEventId;
                DELETE ae FROM dbo.AccountingEvents ae JOIN @events e ON e.AccountingEventId=ae.AccountingEventId WHERE e.IsReversal=1;
                DELETE ae FROM dbo.AccountingEvents ae JOIN @events e ON e.AccountingEventId=ae.AccountingEventId WHERE e.IsReversal=0;
                DELETE FROM dbo.SupplierPaymentAllocations WHERE SupplierPaymentId IN (SELECT SupplierPaymentId FROM dbo.SupplierPayments WHERE SupplierId=@supplierId);
                DELETE FROM dbo.SupplierLedgerEntries WHERE SupplierId=@supplierId;
                DELETE FROM dbo.SupplierPayments WHERE SupplierId=@supplierId;
                DELETE FROM dbo.SupplierInvoices WHERE SupplierId=@supplierId;
                DELETE FROM dbo.PurchaseOrderItems WHERE PurchaseOrderId IN (SELECT PurchaseOrderId FROM dbo.PurchaseOrders WHERE SupplierId=@supplierId);
                DELETE FROM dbo.PurchaseOrders WHERE SupplierId=@supplierId;
                DELETE FROM dbo.Suppliers WHERE SupplierId=@supplierId;
                """, connection, transaction))
            {
                command.Parameters.AddWithValue("@supplierId", fixture.SupplierId);
                await command.ExecuteNonQueryAsync();
            }
            await RestoreFoundationStateAsync(connection, transaction, fixture);
            await using (var accounts = new SqlCommand("DELETE FROM dbo.CashAccounts WHERE CashAccountId=@cashAccountId; DELETE FROM dbo.LedgerAccounts WHERE AccountCode LIKE N'ES5C%' OR AccountCode LIKE N'ES5L%' OR AccountCode LIKE N'ES5P%' OR AccountCode LIKE N'ES5A%';", connection, transaction))
            {
                accounts.Parameters.AddWithValue("@cashAccountId", fixture.CashAccountId);
                await accounts.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<(SupplierFinancialInvoiceResult Invoice, SupplierFinancialPaymentResult Payment)> CreateInvoiceAndAdvanceAsync(CommittedFixture fixture, decimal amount)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var runtime = new SupplierFinancialRuntime();
            var day = new DateOnly(2026, 9, 29);
            var invoice = await runtime.CreateInvoiceAsync(connection, transaction, new SupplierFinancialInvoiceRequest(fixture.SupplierId, $"ES5G-INV-{Guid.NewGuid():N}", day, day, amount, null, Guid.NewGuid(), "es5g-test"), CancellationToken.None);
            var payment = await runtime.PayAsync(connection, transaction, new SupplierFinancialPaymentRequest(fixture.SupplierId, null, amount, day, fixture.CashAccountId, SupplierPaymentKind.Advance, "Cash", $"ES5G-ADV-{Guid.NewGuid():N}", null, Guid.NewGuid(), "es5g-test"), CancellationToken.None);
            await transaction.CommitAsync();
            return (invoice, payment);
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task CreateAdvanceAsync(CommittedFixture fixture, decimal amount, string reference)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            await new SupplierFinancialRuntime().PayAsync(connection, transaction, new SupplierFinancialPaymentRequest(fixture.SupplierId, null, amount, new DateOnly(2026, 9, 29), fixture.CashAccountId, SupplierPaymentKind.Advance, "Cash", reference, null, Guid.NewGuid(), "es5g-test"), CancellationToken.None);
            await transaction.CommitAsync();
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<SupplierFinancialAllocationResult> AllocateCommittedAsync(int paymentId, int invoiceId, decimal amount, Guid operation, string reference)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var result = await new SupplierFinancialRuntime().AllocateAsync(connection, transaction, paymentId, invoiceId, amount, new DateOnly(2026, 9, 29), operation, reference, "es5g-test", CancellationToken.None);
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<bool> AttemptAllocationAsync(int paymentId, int invoiceId, Guid operation, string reference, TaskCompletionSource ready, Task start)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            ready.SetResult();
            await start;
            await new SupplierFinancialRuntime().AllocateAsync(connection, transaction, paymentId, invoiceId, 100m, new DateOnly(2026, 9, 29), operation, reference, "es5g-test", CancellationToken.None);
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            return false;
        }
    }

    private static async Task<SupplierFinancialInvoiceResult> CreateConcurrentInvoiceAsync(CommittedFixture fixture, string number, TaskCompletionSource ready, Task start)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            ready.SetResult();
            await start;
            var result = await new SupplierFinancialRuntime().CreateInvoiceAsync(connection, transaction, new SupplierFinancialInvoiceRequest(fixture.SupplierId, number, new DateOnly(2026, 9, 29), new DateOnly(2026, 9, 29), 100m, null, Guid.NewGuid(), "es5g-test"), CancellationToken.None);
            await transaction.CommitAsync();
            return result;
        }
        catch
        {
            if (transaction.Connection is not null) await transaction.RollbackAsync();
            throw;
        }
    }

    private static async Task<(IReadOnlyList<MappingState> Mappings, IReadOnlyList<EventState> Events)> ReadFoundationStateAsync(SqlConnection connection, SqlTransaction transaction)
    {
        await using var command = new SqlCommand("SELECT AccountRole,LedgerAccountId,IsEnabled FROM dbo.AccountRoleMappings WHERE AccountRole IN(N'Cash',N'SupplierLiability',N'PurchaseClearing',N'SupplierAdvance'); SELECT AccountingEventType,IsEnabled FROM dbo.AccountingEventDefinitions WHERE AccountingEventType IN(28,29,30,31,32,33);", connection, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        var mappings = new List<MappingState>();
        while (await reader.ReadAsync()) mappings.Add(new MappingState(reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.GetBoolean(2)));
        await reader.NextResultAsync();
        var events = new List<EventState>();
        while (await reader.ReadAsync()) events.Add(new EventState(reader.GetByte(0), reader.GetBoolean(1)));
        return (mappings, events);
    }

    private static async Task RestoreFoundationStateAsync(SqlConnection connection, SqlTransaction transaction, CommittedFixture fixture)
    {
        foreach (var mapping in fixture.Mappings)
        {
            await using var command = new SqlCommand("UPDATE dbo.AccountRoleMappings SET LedgerAccountId=@ledgerAccountId,IsEnabled=@isEnabled WHERE AccountRole=@role;", connection, transaction);
            command.Parameters.AddWithValue("@ledgerAccountId", mapping.LedgerAccountId ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@isEnabled", mapping.IsEnabled);
            command.Parameters.AddWithValue("@role", mapping.Role);
            await command.ExecuteNonQueryAsync();
        }
        foreach (var definition in fixture.Events)
        {
            await using var command = new SqlCommand("UPDATE dbo.AccountingEventDefinitions SET IsEnabled=@isEnabled WHERE AccountingEventType=@eventType;", connection, transaction);
            command.Parameters.AddWithValue("@isEnabled", definition.IsEnabled);
            command.Parameters.AddWithValue("@eventType", definition.EventType);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task<int> CountForSupplierAsync(string sql, int supplierId)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@supplierId", supplierId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<decimal> ScalarForSupplierAsync(string sql, int supplierId)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@supplierId", supplierId);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static async Task<decimal> ScalarForInvoiceAsync(string sql, int invoiceId)
    {
        await using var connection = new SqlConnection(ConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@invoiceId", invoiceId);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private sealed record CommittedFixture(int SupplierId, int CashAccountId, IReadOnlyList<MappingState> Mappings, IReadOnlyList<EventState> Events);
    private sealed record MappingState(string Role, int? LedgerAccountId, bool IsEnabled);
    private sealed record EventState(byte EventType, bool IsEnabled);

    private static async Task<int> InsertSupplierAsync(SqlConnection connection, SqlTransaction transaction)
    {
        await using var command = new SqlCommand("INSERT dbo.Suppliers(SupplierCode,SupplierName,IsActive,CreatedAt) OUTPUT inserted.SupplierId VALUES(@code,N'مورد اختبار ES5',1,SYSUTCDATETIME());", connection, transaction);
        command.Parameters.AddWithValue("@code", $"ES5-{Guid.NewGuid():N}"[..16]);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ConfigureFoundationAsync(SqlConnection connection, SqlTransaction transaction)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        const string sql = """
            DECLARE @accounts table (AccountCode nvarchar(50), LedgerAccountId int);
            INSERT dbo.LedgerAccounts(AccountCode,AccountName,AccountType,IsActive,CreatedAt)
            OUTPUT inserted.AccountCode,inserted.LedgerAccountId INTO @accounts
            VALUES (@cash,N'نقد',N'Asset',1,SYSUTCDATETIME()),(@liability,N'دائنون',N'Liability',1,SYSUTCDATETIME()),(@clearing,N'مشتريات',N'Asset',1,SYSUTCDATETIME()),(@advance,N'سلف موردين',N'Asset',1,SYSUTCDATETIME());
            UPDATE dbo.AccountRoleMappings SET LedgerAccountId=(SELECT LedgerAccountId FROM @accounts WHERE AccountCode=CASE AccountRole WHEN N'Cash' THEN @cash WHEN N'SupplierLiability' THEN @liability WHEN N'PurchaseClearing' THEN @clearing WHEN N'SupplierAdvance' THEN @advance END),IsEnabled=1 WHERE AccountRole IN(N'Cash',N'SupplierLiability',N'PurchaseClearing',N'SupplierAdvance');
            UPDATE dbo.AccountingEventDefinitions SET IsEnabled=1 WHERE AccountingEventType IN(28,29,30,31,32,33);
            INSERT dbo.CashAccounts(AccountName,CurrentBalance,IsActive,CreatedAt,CashAccountType,CurrencyCode,LedgerControlAccountId,AllowsReceipts,AllowsDisbursements)
            OUTPUT inserted.CashAccountId VALUES(@cashName,0,1,SYSUTCDATETIME(),1,N'YER',(SELECT LedgerAccountId FROM @accounts WHERE AccountCode=@cash),1,1);
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@cash", $"ES5C{suffix}"); command.Parameters.AddWithValue("@liability", $"ES5L{suffix}");
        command.Parameters.AddWithValue("@clearing", $"ES5P{suffix}"); command.Parameters.AddWithValue("@advance", $"ES5A{suffix}"); command.Parameters.AddWithValue("@cashName", $"ES5 Cash {suffix}");
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<int> ScalarIntAsync(SqlConnection connection, SqlTransaction transaction, string sql, int paymentId)
    {
        await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("@paymentId", paymentId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<decimal> ScalarDecimalAsync(SqlConnection connection, SqlTransaction transaction, string sql, long eventId)
    {
        await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("@eventId", eventId);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static async Task<decimal> ScalarDecimalAsync(SqlConnection connection, SqlTransaction transaction, string sql, int invoiceId)
    {
        await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("@invoiceId", invoiceId);
        return Convert.ToDecimal(await command.ExecuteScalarAsync());
    }

    private static async Task<int> CountAsync(SqlConnection connection, SqlTransaction transaction, string sql, long eventId)
    {
        await using var command = new SqlCommand(sql, connection, transaction); command.Parameters.AddWithValue("@eventId", eventId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static string ConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("Lumar__ConnectionString") ?? "Server=YASIN-YASIN\\SQLEXPRESS;Database=LUMAR_ERP_TEST;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True");
        if (!string.Equals(builder.InitialCatalog, "LUMAR_ERP_TEST", StringComparison.OrdinalIgnoreCase) && !string.Equals(builder.InitialCatalog, "LUMAR_ERP_ES_VALIDATION", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("ES-5 tests require an approved ES validation database.");
        return builder.ConnectionString;
    }
}