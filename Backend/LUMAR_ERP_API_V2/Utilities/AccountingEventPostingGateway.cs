using System.Data;
using Microsoft.Data.SqlClient;

namespace LUMAR_ERP_API_V2.Utilities;

public enum AccountingEventType : byte
{
    CustomerAdvance = 1,
    CustomerPayment = 2,
    RevenueRecognizedOrder = 3,
    RevenueRecognizedPrintSale = 4,
    WipToFinishedGoods = 5,
    CustomerAdvanceApplied = 6,
    FabricInventoryReceived = 7,
    FabricInventoryConsumed = 8,
    ConsumableInventoryReceived = 9,
    ConsumableInventoryConsumed = 10,
    ReadyMadeSaleCost = 11,
    ImportedReadyMadeInventoryReceived = 12,
    ImportedReadyMadeSaleCost = 13,
    ToolOperationalIssue = 14,
    ToolOperationalReversal = 15
}

public sealed record AccountingEventPostingResult(long AccountingEventId, int FinancialTransactionId, int JournalEntryId, bool IsExisting);

public static class AccountingEventPostingGateway
{
    public static async Task<AccountingEventPostingResult> PostAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        AccountingEventType accountingEventType,
        decimal postingAmount,
        int? paymentId,
        int? orderId,
        int? measurementCardPrintHistoryId,
        int? readyMadeInventoryProductId,
        string? referenceNumber,
        string? description,
        CancellationToken cancellationToken)
        => await PostCoreAsync(
            connection,
            transaction,
            accountingEventType,
            postingAmount,
            paymentId: paymentId,
            orderId: orderId,
            measurementCardPrintHistoryId: measurementCardPrintHistoryId,
            readyMadeInventoryProductId: readyMadeInventoryProductId,
            customerAdvanceApplicationId: null,
            inventoryReceiptPostingId: null,
            fabricConsumptionSourceId: null,
            productionMaterialConsumptionId: null,
            readyMadeSaleCostPostingId: null,
            importedReadyMadeInventoryReceiptId: null,
            importedReadyMadeSaleCostPostingId: null,
            toolIssuanceId: null,
            toolIssuanceReversalId: null,
            referenceNumber: referenceNumber,
            description: description,
            cancellationToken: cancellationToken);

    public static Task<AccountingEventPostingResult> PostInventoryReceiptAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        AccountingEventType accountingEventType,
        long inventoryReceiptPostingId,
        decimal postingAmount,
        string? referenceNumber,
        string? description,
        CancellationToken cancellationToken)
        => PostCoreAsync(
            connection,
            transaction,
            accountingEventType,
            postingAmount,
            paymentId: null,
            orderId: null,
            measurementCardPrintHistoryId: null,
            readyMadeInventoryProductId: null,
            customerAdvanceApplicationId: null,
            inventoryReceiptPostingId: inventoryReceiptPostingId,
            fabricConsumptionSourceId: null,
            productionMaterialConsumptionId: null,
            readyMadeSaleCostPostingId: null,
            importedReadyMadeInventoryReceiptId: null,
            importedReadyMadeSaleCostPostingId: null,
            toolIssuanceId: null,
            toolIssuanceReversalId: null,
            referenceNumber: referenceNumber,
            description: description,
            cancellationToken: cancellationToken);

    public static Task<AccountingEventPostingResult> PostFabricConsumptionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long fabricConsumptionSourceId,
        decimal postingAmount,
        string? referenceNumber,
        string? description,
        CancellationToken cancellationToken)
        => PostCoreAsync(
            connection,
            transaction,
            AccountingEventType.FabricInventoryConsumed,
            postingAmount,
            paymentId: null,
            orderId: null,
            measurementCardPrintHistoryId: null,
            readyMadeInventoryProductId: null,
            customerAdvanceApplicationId: null,
            inventoryReceiptPostingId: null,
            fabricConsumptionSourceId: fabricConsumptionSourceId,
            productionMaterialConsumptionId: null,
            readyMadeSaleCostPostingId: null,
            importedReadyMadeInventoryReceiptId: null,
            importedReadyMadeSaleCostPostingId: null,
            toolIssuanceId: null,
            toolIssuanceReversalId: null,
            referenceNumber: referenceNumber,
            description: description,
            cancellationToken: cancellationToken);

    public static Task<AccountingEventPostingResult> PostConsumableConsumptionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int productionMaterialConsumptionId,
        decimal postingAmount,
        string? referenceNumber,
        string? description,
        CancellationToken cancellationToken)
        => PostCoreAsync(
            connection,
            transaction,
            AccountingEventType.ConsumableInventoryConsumed,
            postingAmount,
            paymentId: null,
            orderId: null,
            measurementCardPrintHistoryId: null,
            readyMadeInventoryProductId: null,
            customerAdvanceApplicationId: null,
            inventoryReceiptPostingId: null,
            fabricConsumptionSourceId: null,
            productionMaterialConsumptionId: productionMaterialConsumptionId,
            readyMadeSaleCostPostingId: null,
            importedReadyMadeInventoryReceiptId: null,
            importedReadyMadeSaleCostPostingId: null,
            toolIssuanceId: null,
            toolIssuanceReversalId: null,
            referenceNumber: referenceNumber,
            description: description,
            cancellationToken: cancellationToken);

    public static Task<AccountingEventPostingResult> PostReadyMadeSaleCostAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long readyMadeSaleCostPostingId,
        decimal postingAmount,
        string? referenceNumber,
        string? description,
        CancellationToken cancellationToken)
        => PostCoreAsync(
            connection,
            transaction,
            AccountingEventType.ReadyMadeSaleCost,
            postingAmount,
            paymentId: null,
            orderId: null,
            measurementCardPrintHistoryId: null,
            readyMadeInventoryProductId: null,
            customerAdvanceApplicationId: null,
            inventoryReceiptPostingId: null,
            fabricConsumptionSourceId: null,
            productionMaterialConsumptionId: null,
            readyMadeSaleCostPostingId: readyMadeSaleCostPostingId,
            importedReadyMadeInventoryReceiptId: null,
            importedReadyMadeSaleCostPostingId: null,
            toolIssuanceId: null,
            toolIssuanceReversalId: null,
            referenceNumber: referenceNumber,
            description: description,
            cancellationToken: cancellationToken);

    public static Task<AccountingEventPostingResult> PostImportedInventoryReceiptAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long importedReadyMadeInventoryReceiptId,
        decimal postingAmount,
        string? referenceNumber,
        string? description,
        CancellationToken cancellationToken)
        => PostCoreAsync(
            connection,
            transaction,
            AccountingEventType.ImportedReadyMadeInventoryReceived,
            postingAmount,
            paymentId: null,
            orderId: null,
            measurementCardPrintHistoryId: null,
            readyMadeInventoryProductId: null,
            customerAdvanceApplicationId: null,
            inventoryReceiptPostingId: null,
            fabricConsumptionSourceId: null,
            productionMaterialConsumptionId: null,
            readyMadeSaleCostPostingId: null,
            importedReadyMadeInventoryReceiptId: importedReadyMadeInventoryReceiptId,
            importedReadyMadeSaleCostPostingId: null,
            toolIssuanceId: null,
            toolIssuanceReversalId: null,
            referenceNumber: referenceNumber,
            description: description,
            cancellationToken: cancellationToken);

    public static Task<AccountingEventPostingResult> PostImportedSaleCostAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long importedReadyMadeSaleCostPostingId,
        decimal postingAmount,
        string? referenceNumber,
        string? description,
        CancellationToken cancellationToken)
        => PostCoreAsync(
            connection,
            transaction,
            AccountingEventType.ImportedReadyMadeSaleCost,
            postingAmount,
            paymentId: null,
            orderId: null,
            measurementCardPrintHistoryId: null,
            readyMadeInventoryProductId: null,
            customerAdvanceApplicationId: null,
            inventoryReceiptPostingId: null,
            fabricConsumptionSourceId: null,
            productionMaterialConsumptionId: null,
            readyMadeSaleCostPostingId: null,
            importedReadyMadeInventoryReceiptId: null,
            importedReadyMadeSaleCostPostingId: importedReadyMadeSaleCostPostingId,
            toolIssuanceId: null,
            toolIssuanceReversalId: null,
            referenceNumber: referenceNumber,
            description: description,
            cancellationToken: cancellationToken);

    public static Task<AccountingEventPostingResult> PostToolOperationalIssueAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long toolIssuanceId,
        decimal postingAmount,
        string? referenceNumber,
        string? description,
        CancellationToken cancellationToken)
        => PostCoreAsync(
            connection,
            transaction,
            AccountingEventType.ToolOperationalIssue,
            postingAmount,
            paymentId: null,
            orderId: null,
            measurementCardPrintHistoryId: null,
            readyMadeInventoryProductId: null,
            customerAdvanceApplicationId: null,
            inventoryReceiptPostingId: null,
            fabricConsumptionSourceId: null,
            productionMaterialConsumptionId: null,
            readyMadeSaleCostPostingId: null,
            importedReadyMadeInventoryReceiptId: null,
            importedReadyMadeSaleCostPostingId: null,
            toolIssuanceId: toolIssuanceId,
            toolIssuanceReversalId: null,
            referenceNumber: referenceNumber,
            description: description,
            cancellationToken: cancellationToken);

    public static Task<AccountingEventPostingResult> PostToolOperationalReversalAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long toolIssuanceReversalId,
        decimal postingAmount,
        string? referenceNumber,
        string? description,
        CancellationToken cancellationToken)
        => PostCoreAsync(
            connection,
            transaction,
            AccountingEventType.ToolOperationalReversal,
            postingAmount,
            paymentId: null,
            orderId: null,
            measurementCardPrintHistoryId: null,
            readyMadeInventoryProductId: null,
            customerAdvanceApplicationId: null,
            inventoryReceiptPostingId: null,
            fabricConsumptionSourceId: null,
            productionMaterialConsumptionId: null,
            readyMadeSaleCostPostingId: null,
            importedReadyMadeInventoryReceiptId: null,
            importedReadyMadeSaleCostPostingId: null,
            toolIssuanceId: null,
            toolIssuanceReversalId: toolIssuanceReversalId,
            referenceNumber: referenceNumber,
            description: description,
            cancellationToken: cancellationToken);

    public static Task<AccountingEventPostingResult> PostCustomerAdvanceApplicationAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        long advanceApplicationId,
        decimal postingAmount,
        string? referenceNumber,
        string? description,
        CancellationToken cancellationToken)
        => PostCoreAsync(
            connection,
            transaction,
            AccountingEventType.CustomerAdvanceApplied,
            postingAmount,
            paymentId: null,
            orderId: null,
            measurementCardPrintHistoryId: null,
            readyMadeInventoryProductId: null,
            customerAdvanceApplicationId: advanceApplicationId,
            inventoryReceiptPostingId: null,
            fabricConsumptionSourceId: null,
            productionMaterialConsumptionId: null,
            readyMadeSaleCostPostingId: null,
            importedReadyMadeInventoryReceiptId: null,
            importedReadyMadeSaleCostPostingId: null,
            toolIssuanceId: null,
            toolIssuanceReversalId: null,
            referenceNumber: referenceNumber,
            description: description,
            cancellationToken: cancellationToken);

    private static async Task<AccountingEventPostingResult> PostCoreAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        AccountingEventType accountingEventType,
        decimal postingAmount,
        int? paymentId,
        int? orderId,
        int? measurementCardPrintHistoryId,
        int? readyMadeInventoryProductId,
        long? customerAdvanceApplicationId,
        long? inventoryReceiptPostingId,
        long? fabricConsumptionSourceId,
        int? productionMaterialConsumptionId,
        long? readyMadeSaleCostPostingId,
        long? importedReadyMadeInventoryReceiptId,
        long? importedReadyMadeSaleCostPostingId,
        long? toolIssuanceId,
        long? toolIssuanceReversalId,
        string? referenceNumber,
        string? description,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("dbo.usp_PostAccountingEvent", connection, transaction)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@AccountingEventType", SqlDbType.TinyInt).Value = (byte)accountingEventType;
        var postingAmountParameter = command.Parameters.Add("@PostingAmount", SqlDbType.Decimal);
        postingAmountParameter.Precision = 18;
        postingAmountParameter.Scale = 2;
        postingAmountParameter.Value = postingAmount;
        AddNullableInt(command, "@PaymentId", paymentId);
        AddNullableInt(command, "@OrderId", orderId);
        AddNullableInt(command, "@MeasurementCardPrintHistoryId", measurementCardPrintHistoryId);
        AddNullableInt(command, "@ReadyMadeInventoryProductId", readyMadeInventoryProductId);
        AddNullableLong(command, "@CustomerAdvanceApplicationId", customerAdvanceApplicationId);
        AddNullableLong(command, "@InventoryReceiptPostingId", inventoryReceiptPostingId);
        AddNullableLong(command, "@FabricConsumptionSourceId", fabricConsumptionSourceId);
        AddNullableInt(command, "@ProductionMaterialConsumptionId", productionMaterialConsumptionId);
        AddNullableLong(command, "@ReadyMadeSaleCostPostingId", readyMadeSaleCostPostingId);
        AddNullableLong(command, "@ImportedReadyMadeInventoryReceiptId", importedReadyMadeInventoryReceiptId);
        AddNullableLong(command, "@ImportedReadyMadeSaleCostPostingId", importedReadyMadeSaleCostPostingId);
        AddNullableLong(command, "@ToolIssuanceId", toolIssuanceId);
        AddNullableLong(command, "@ToolIssuanceReversalId", toolIssuanceReversalId);
        AddNullableString(command, "@ReferenceNumber", referenceNumber, 200);
        AddNullableString(command, "@Description", description, 1000);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("تعذر إنشاء الحدث المحاسبي الرسمي.");

        var result = new AccountingEventPostingResult(
            reader.GetInt64(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetBoolean(3));
        await reader.DisposeAsync();

        if (!result.IsExisting && accountingEventType is (AccountingEventType.WipToFinishedGoods
            or AccountingEventType.FabricInventoryConsumed
            or AccountingEventType.ReadyMadeSaleCost
            or AccountingEventType.ImportedReadyMadeSaleCost))
        {
            const string statusSql = @"
                UPDATE dbo.AccountingEvents
                SET Status = N'Posted'
                WHERE AccountingEventId = @accountingEventId
                  AND AccountingEventType = @accountingEventType
                  AND Status IN (N'Legacy', N'Posted');";
            await using var statusCommand = new SqlCommand(statusSql, connection, transaction);
            statusCommand.Parameters.AddWithValue("@accountingEventId", result.AccountingEventId);
            statusCommand.Parameters.Add("@accountingEventType", SqlDbType.TinyInt).Value = (byte)accountingEventType;
            if (await statusCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("تعذر تثبيت حالة الحدث المحاسبي القابل للعكس.");
        }

        return result;
    }

    private static void AddNullableInt(SqlCommand command, string name, int? value) =>
        command.Parameters.Add(name, SqlDbType.Int).Value = value ?? (object)DBNull.Value;

    private static void AddNullableLong(SqlCommand command, string name, long? value) =>
        command.Parameters.Add(name, SqlDbType.BigInt).Value = value ?? (object)DBNull.Value;

    private static void AddNullableString(SqlCommand command, string name, string? value, int size) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
}