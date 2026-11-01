using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.DTOs.Loyalty;
using LUMAR_ERP_API_V2.DTOs.Orders;
using LUMAR_ERP_API_V2.DTOs.Pricing;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed partial class FabricConsumablesFoundationIntegrationTests
{
    [Fact]
    public async Task TailoringEdit_AddCancelAndReplay_PreserveIdentityAndApplyOnlyDeltas()
    {
        var connectionString=GetConnectionString();
        Assert.Equal("LUMAR_TAILORING_EDIT_TEST_20261101",new SqlConnectionStringBuilder(connectionString).InitialCatalog);
        var seed=await SeedAsync(connectionString);
        var receipt=await ReceiveOrderFabricAsync(connectionString,seed);
        var options=Options.Create(new DatabaseOptions { ConnectionString=connectionString });
        var readConnections=new ReadOnlySqlConnectionFactory(options);
        var writeConnections=new OperationalSqlConnectionFactory(options);
        var pricing=new EditTestPricing { Price=20000m };
        var points=new EditTestPoints();
        var repository=new OrderRepository(readConnections,writeConnections,new MeasurementConsumptionRules(),pricing,points);
        var loyalty=new LoyaltyRepository(readConnections,writeConnections);
        var referrals=new ReferralRepository(readConnections,writeConnections);
        var parentId=await CreateEditReferrerAsync(connectionString,seed.CustomerId);
        var loyaltyService=new OrderLoyaltyIntegrationService(repository,points,referrals,loyalty);
        var service=new OrderService(repository,null!,loyaltyService);
        var cashId=await ReadIntAsync(connectionString,
            "SELECT TOP(1) CashAccountId FROM dbo.CashAccounts WHERE IsActive=1 AND AllowsReceipts=1 AND AllowsDisbursements=1 AND CurrencyCode=N'YER' ORDER BY CashAccountId",0);
        var template=await CreateFabricOrderRequestAsync(connectionString,seed,$"EDIT-{Guid.NewGuid():N}",36m,1);
        var created=await service.CreateAsync(new CreateOrderDto
        {
            CustomerId=template.CustomerId,RequestReference=template.RequestReference,Items=template.Items,
            TotalAmount=20000m,AdvancePayment=20000m,CashAccountId=cashId,PaymentMethod="Cash"
        },CancellationToken.None);
        Assert.NotNull(created);
        var before=await repository.GetEditSnapshotAsync(created.OrderId,CancellationToken.None);
        Assert.NotNull(before);
        var originalPiece=Assert.Single(before.Pieces);
        Assert.Equal(20000m,originalPiece.OriginalSalePrice);
        Assert.Equal(100m,await BuyerPointsAsync());
        Assert.Equal(50m,await ReferralNetAsync());
        var paymentCount=await CountAsync(connectionString,"SELECT COUNT(*) FROM dbo.Payments WHERE OrderID=@id",created.OrderId);

        pricing.Price=15406.25m;
        var addition=new TailoringNewPiece
        {
            AddOperationId=Guid.NewGuid(),ProductTypeId=originalPiece.ProductTypeId,FabricCode=seed.FabricItemCode,
            Measurements=new Dictionary<string,string> { ["Length"]="36" }
        };
        var addRequest=new TailoringOrderEditRequest
        {
            EditOperationId=Guid.NewGuid(),ExpectedVersion=before.Version,AddedPieces=[addition]
        };
        var added=await repository.ApplyEditAsync(created.OrderId,addRequest,"Test",CancellationToken.None);
        Assert.Equal(created.OrderId,added.Order.OrderId);
        Assert.Equal(created.OrderNumber,added.Order.OrderNumber);
        Assert.Equal(seed.CustomerId,added.Order.CustomerId);
        Assert.Equal(35406.25m,added.Order.TotalAmount);
        Assert.Equal(20000m,added.Order.PaidAmount);
        Assert.Equal(15406.25m,added.Order.RemainingAmount);
        Assert.Equal(2,added.Pieces.Count);
        Assert.Equal(200m,await BuyerPointsAsync());
        Assert.Equal(100m,await ReferralNetAsync());
        Assert.Equal(4m,await AvailableAsync());
        Assert.Equal(paymentCount,await CountAsync(connectionString,"SELECT COUNT(*) FROM dbo.Payments WHERE OrderID=@id",created.OrderId));
        var replay=await repository.ApplyEditAsync(created.OrderId,addRequest,"Test",CancellationToken.None);
        Assert.Equal(added.Version,replay.Version);
        Assert.Equal(added.AddedPieceIds[addition.AddOperationId],replay.AddedPieceIds[addition.AddOperationId]);
        Assert.Equal(200m,await BuyerPointsAsync());
        Assert.Equal(4m,await AvailableAsync());
        await Assert.ThrowsAsync<TailoringOrderEditConflictException>(() => repository.ApplyEditAsync(created.OrderId,
            addRequest with { EditOperationId=Guid.NewGuid() },"Test",CancellationToken.None));

        var cancelRequest=new TailoringOrderEditRequest
        {
            EditOperationId=Guid.NewGuid(),ExpectedVersion=added.Version,
            CancelledPieces=[new TailoringPieceCancellation { PieceId=originalPiece.PieceId,Reason="Test cancellation" }]
        };
        var cancelled=await repository.ApplyEditAsync(created.OrderId,cancelRequest,"Test",CancellationToken.None);
        Assert.Equal(created.OrderId,cancelled.Order.OrderId);
        Assert.Equal(created.OrderNumber,cancelled.Order.OrderNumber);
        Assert.Equal(15406.25m,cancelled.Order.TotalAmount);
        Assert.Equal(15406.25m,cancelled.Order.PaidAmount);
        Assert.Equal(0m,cancelled.Order.RemainingAmount);
        Assert.Equal("Cancelled",cancelled.Pieces.Single(piece=>piece.PieceId==originalPiece.PieceId).PieceStatus);
        Assert.Equal(2,cancelled.Pieces.Count);
        Assert.Equal(100m,await BuyerPointsAsync());
        Assert.Equal(50m,await ReferralNetAsync());
        Assert.Equal(5m,await AvailableAsync());
        Assert.Equal(4593.75m,await ReadDecimalAsync(connectionString,
            "SELECT SUM(Amount) FROM dbo.Payments WHERE OrderID=@id AND PaymentKind=N'Refund'",created.OrderId));
        var replayCancel=await repository.ApplyEditAsync(created.OrderId,cancelRequest,"Test",CancellationToken.None);
        Assert.Equal(cancelled.Version,replayCancel.Version);
        Assert.Equal(paymentCount+1,await CountAsync(connectionString,"SELECT COUNT(*) FROM dbo.Payments WHERE OrderID=@id",created.OrderId));
        Assert.Equal(100m,await BuyerPointsAsync());
        Assert.Equal(5m,await AvailableAsync());
        var unchanged=await repository.ApplyEditAsync(created.OrderId,new TailoringOrderEditRequest
        { EditOperationId=Guid.NewGuid(),ExpectedVersion=cancelled.Version },"Test",CancellationToken.None);
        Assert.Equal(cancelled.Version,unchanged.Version);
        Assert.Equal(2,await CountAsync(connectionString,"SELECT COUNT(*) FROM dbo.TailoringOrderEditOperations WHERE OrderId=@id",created.OrderId));
        Assert.Equal(0,await CountAsync(connectionString,"SELECT COUNT(*) FROM dbo.PieceWageRecords WHERE OrderID=@id",created.OrderId));
        Assert.Equal(0,await CountAsync(connectionString,"SELECT COUNT(*) FROM dbo.TrackingEvents WHERE OrderID=@id",created.OrderId));

        Task<decimal> BuyerPointsAsync()=>ReadDecimalAsync(connectionString,
            "SELECT ISNULL(SUM(Points),0) FROM dbo.LoyaltyTransactions WHERE CustomerId=@id",seed.CustomerId);
        Task<decimal> ReferralNetAsync()=>ReadDecimalAsync(connectionString,
            "SELECT ISNULL(SUM(CASE WHEN TransactionType=N'RewardGranted' THEN LoyaltyPoints WHEN TransactionType=N'RewardReversal' THEN -LoyaltyPoints ELSE 0 END),0) FROM dbo.ReferralTransactions WHERE ReferrerCustomerId=@id",parentId);
        Task<decimal> AvailableAsync()=>ReadDecimalAsync(connectionString,
            "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id",receipt.InventoryItemId);
    }

    private static async Task<int> CreateEditReferrerAsync(string connectionString,int customerId)
    {
        await using var connection=new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction=(SqlTransaction)await connection.BeginTransactionAsync();
        var parentId=await InsertScalarAsync(connection,transaction,"""
            INSERT dbo.Customers(CustomerCode,CustomerName,PhoneNumber,TotalPoints,TotalPieces,TotalDebts,IsActive)
            OUTPUT INSERTED.CustomerID VALUES(@code,N'Edit test referrer',N'000000000',0,0,0,1)
            """,("@code",$"ER-{Guid.NewGuid():N}"[..18]));
        await using var registration=new SqlCommand("""
            INSERT dbo.ReferralTransactions(ReferrerCustomerId,ReferredCustomerId,TransactionType,FixedRewardAmount,LoyaltyPoints,CreatedAt)
            VALUES(@parent,@customer,N'Registration',0,0,SYSUTCDATETIME())
            """,connection,transaction);
        registration.Parameters.AddWithValue("@parent",parentId);
        registration.Parameters.AddWithValue("@customer",customerId);
        await registration.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        return parentId;
    }

    private sealed class EditTestPricing : IPricingEngineService
    {
        public decimal Price { get; set; }=1000m;
        public Task<PricingEngineResponseDto> CalculateAsync(PricingEngineRequestDto request,CancellationToken cancellationToken)=>
            Task.FromResult(new PricingEngineResponseDto(true,[],request.ProductTypeId,request.FabricCode,"Inch",
                request.Consumption,request.Quantity,1m,request.Consumption,request.Consumption*request.Quantity,
                1m,1m,1m,1m,4m,request.Consumption+4m,(request.Consumption+4m)*request.Quantity,
                0m,0m,request.Consumption+4m,0m,0m,Price,Price*request.Quantity,"Test","Test"));
    }

    private sealed class EditTestPoints : IPiecePointSettingsRepository
    {
        public Task<LoyaltyPiecePointSettingDto?> GetActiveProductPointSettingAsync(int productTypeId,CancellationToken cancellationToken)=>
            Task.FromResult<LoyaltyPiecePointSettingDto?>(new(1,"Test","Test",100m,true,DateTime.UtcNow,DateTime.UtcNow,productTypeId));
        public Task<LoyaltyProgramSettingsDto?> GetProgramSettingsAsync(CancellationToken cancellationToken)=>
            Task.FromResult<LoyaltyProgramSettingsDto?>(new(1,true,100m,1m,DateTime.UtcNow,DateTime.UtcNow));
        public Task<IReadOnlyList<ProductLoyaltyPointSettingDto>> GetProductLoyaltyPointSettingsAsync(CancellationToken cancellationToken)=>throw new NotSupportedException();
        public Task<ProductLoyaltyPointSettingDto?> UpsertProductLoyaltyPointSettingAsync(int id,UpdateProductLoyaltyPointSettingDto request,CancellationToken cancellationToken)=>throw new NotSupportedException();
        public Task<IReadOnlyList<ReadyMadeProductTypeLoyaltyPointSettingDto>> GetReadyMadeProductTypeLoyaltyPointSettingsAsync(CancellationToken cancellationToken)=>throw new NotSupportedException();
        public Task<ReadyMadeProductTypeLoyaltyPointSettingDto?> UpsertReadyMadeProductTypeLoyaltyPointSettingAsync(int productTypeId,UpdateReadyMadeProductTypeLoyaltyPointSettingDto request,CancellationToken cancellationToken)=>throw new NotSupportedException();
        public Task<IReadOnlyList<ImportedProductLoyaltyPointSettingDto>> GetImportedProductLoyaltyPointSettingsAsync(CancellationToken cancellationToken)=>throw new NotSupportedException();
        public Task<ImportedProductLoyaltyPointSettingDto?> UpsertImportedProductLoyaltyPointSettingAsync(int importedReadyMadeProductId,UpdateImportedProductLoyaltyPointSettingDto request,CancellationToken cancellationToken)=>throw new NotSupportedException();
        public Task<IReadOnlyList<ReadyMadeProductTypeLoyaltyPointSettingDto>> GetReadyMadeProductTypePointSettingsAsync(CancellationToken cancellationToken)=>throw new NotSupportedException();
        public Task<ReadyMadeProductTypeLoyaltyPointSettingDto?> UpsertReadyMadeProductTypePointSettingAsync(int id,UpdateReadyMadeProductTypeLoyaltyPointSettingDto request,CancellationToken cancellationToken)=>throw new NotSupportedException();
        public Task<LoyaltyPiecePointSettingDto?> GetActiveReadyMadeProductTypePointSettingAsync(int id,CancellationToken cancellationToken)=>throw new NotSupportedException();
        public Task<LoyaltyPiecePointSettingDto?> GetActiveImportedPointSettingAsync(int id,CancellationToken cancellationToken)=>throw new NotSupportedException();
        public Task<LoyaltyPiecePointSettingDto?> GetActivePieceSettingAsync(string code,CancellationToken cancellationToken)=>throw new NotSupportedException();
        public Task<PiecePointsResultDto> EvaluatePiecePointsAsync(int customerId,int orderId,string code,decimal quantity,string source,string? notes,CancellationToken cancellationToken)=>throw new NotSupportedException();
    }
}