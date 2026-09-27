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
    ImportedReadyMadeSaleCost = 13
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
            paymentId,
            orderId,
            measurementCardPrintHistoryId,
            readyMadeInventoryProductId,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            referenceNumber,
            description,
            cancellationToken);

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
            null,
            null,
            null,
            null,
            null,
            inventoryReceiptPostingId,
            null,
            null,
            null,
            null,
            null,
            referenceNumber,
            description,
            cancellationToken);

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
            null,
            null,
            null,
            null,
            null,
            null,
            fabricConsumptionSourceId,
            null,
            null,
            null,
            null,
            referenceNumber,
            description,
            cancellationToken);

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
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            productionMaterialConsumptionId,
            null,
            null,
            null,
            referenceNumber,
            description,
            cancellationToken);

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
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            readyMadeSaleCostPostingId,
            null,
            null,
            referenceNumber,
            description,
            cancellationToken);

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
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            importedReadyMadeInventoryReceiptId,
            null,
            referenceNumber,
            description,
            cancellationToken);

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
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            importedReadyMadeSaleCostPostingId,
            referenceNumber,
            description,
            cancellationToken);

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
            null,
            null,
            null,
            null,
            advanceApplicationId,
            null,
            null,
            null,
            null,
            null,
            null,
            referenceNumber,
            description,
            cancellationToken);

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
        AddNullableString(command, "@ReferenceNumber", referenceNumber, 200);
        AddNullableString(command, "@Description", description, 1000);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("تعذر إنشاء الحدث المحاسبي الرسمي.");

        return new AccountingEventPostingResult(
            reader.GetInt64(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetBoolean(3));
    }

    private static void AddNullableInt(SqlCommand command, string name, int? value) =>
        command.Parameters.Add(name, SqlDbType.Int).Value = value ?? (object)DBNull.Value;

    private static void AddNullableLong(SqlCommand command, string name, long? value) =>
        command.Parameters.Add(name, SqlDbType.BigInt).Value = value ?? (object)DBNull.Value;

    private static void AddNullableString(SqlCommand command, string name, string? value, int size) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, size).Value = string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
}