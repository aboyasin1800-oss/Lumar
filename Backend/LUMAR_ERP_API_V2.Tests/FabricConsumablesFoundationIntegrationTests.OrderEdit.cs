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
    public async Task TailoringEdit_AddPiece_AddsOfficialPriceToExistingTotal_WhenLegacyPieceHasNoPriceSnapshot()
    {
        var connectionString = new SqlConnectionStringBuilder(GetConnectionString())
        {
            InitialCatalog = "LUMAR_TAILORING_EDIT_TEST_20261101"
        }.ConnectionString;
        var seed = await SeedAsync(connectionString);
        await ReceiveOrderFabricAsync(connectionString, seed);
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var pricing = new EditTestPricing { Price = 15375m };
        var repository = new OrderRepository(
            new ReadOnlySqlConnectionFactory(options),
            new OperationalSqlConnectionFactory(options),
            new MeasurementConsumptionRules(),
            pricing);
        var cashId = await ReadIntAsync(connectionString,
            "SELECT TOP(1) CashAccountId FROM dbo.CashAccounts WHERE IsActive=1 AND AllowsReceipts=1 AND AllowsDisbursements=1 AND CurrencyCode=N'YER' ORDER BY CashAccountId", 0);
        var template = await CreateFabricOrderRequestAsync(connectionString, seed, $"EDIT-{Guid.NewGuid():N}", 36m, 1);
        var created = await repository.CreateAsync(new CreateOrderDto
        {
            CustomerId = template.CustomerId,
            RequestReference = template.RequestReference,
            TotalAmount = 15375m,
            AdvancePayment = 15000m,
            CashAccountId = cashId,
            PaymentMethod = "Cash",
            SaleCategory = "TailoringOrder",
            Items = template.Items
        }, CancellationToken.None);
        Assert.NotNull(created);

        var initial = await repository.GetEditSnapshotAsync(created.OrderId, CancellationToken.None);
        Assert.NotNull(initial);
        var originalPiece = Assert.Single(initial.Pieces);
        await using (var connection = new SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            await using var deleteSnapshot = new SqlCommand(
                "DELETE FROM dbo.TailoringPieceSaleValues WHERE PieceID=@pieceId", connection);
            deleteSnapshot.Parameters.AddWithValue("@pieceId", originalPiece.PieceId);
            Assert.Equal(1, await deleteSnapshot.ExecuteNonQueryAsync());
        }

        var before = await repository.GetEditSnapshotAsync(created.OrderId, CancellationToken.None);
        Assert.NotNull(before);
        Assert.Equal(15375m, before.Order.TotalAmount);
        Assert.Equal(0m, before.Order.DiscountAmount);
        Assert.Equal(15000m, before.Order.PaidAmount);
        Assert.Equal(375m, before.Order.RemainingAmount);
        Assert.Null(Assert.Single(before.Pieces).OriginalSalePrice);

        pricing.Price = 5532.50m;
        var addition = new TailoringNewPiece
        {
            AddOperationId = Guid.NewGuid(),
            ProductTypeId = originalPiece.ProductTypeId,
            FabricCode = seed.FabricItemCode,
            Measurements = new Dictionary<string, string> { ["Length"] = "36" }
        };
        var added = await repository.ApplyEditAsync(created.OrderId, new TailoringOrderEditRequest
        {
            EditOperationId = Guid.NewGuid(),
            ExpectedVersion = before.Version,
            AddedPieces = [addition]
        }, "Test", CancellationToken.None);

        Assert.Equal(created.OrderId, added.Order.OrderId);
        Assert.Equal(created.OrderNumber, added.Order.OrderNumber);
        Assert.Equal(seed.CustomerId, added.Order.CustomerId);
        Assert.Equal(20907.50m, added.Order.TotalAmount);
        Assert.Equal(0m, added.Order.DiscountAmount);
        Assert.Equal(15000m, added.Order.PaidAmount);
        Assert.Equal(5907.50m, added.Order.RemainingAmount);
        Assert.Equal(2, added.Pieces.Count);
        Assert.Contains(added.Pieces, piece => piece.PieceId == originalPiece.PieceId);
        var addedPieceId = added.AddedPieceIds[addition.AddOperationId];
        var addedPiece = Assert.Single(added.Pieces, piece => piece.PieceId == addedPieceId);
        Assert.Equal(5532.50m, addedPiece.OriginalSalePrice);
        Assert.Equal(5532.50m, addedPiece.ApprovedSalePrice);
    }

    [Fact]
    public async Task TailoringEdit_CancelPiece_PreservesActualPaidDiscountAndRemaining()
    {
        var connectionString=GetConnectionString();
        Assert.Equal("LUMAR_TAILORING_EDIT_TEST_20261101",new SqlConnectionStringBuilder(connectionString).InitialCatalog);
        var seed=await SeedAsync(connectionString);
        var receipt=await ReceiveOrderFabricAsync(connectionString,seed);
        var options=Options.Create(new DatabaseOptions { ConnectionString=connectionString });
        var readConnections=new ReadOnlySqlConnectionFactory(options);
        var writeConnections=new OperationalSqlConnectionFactory(options);
        var pricing=new EditTestPricing { Price=6782.50m };
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
            TotalAmount=6782.50m,DiscountAmount=782.50m,AdvancePayment=5000m,CashAccountId=cashId,PaymentMethod="Cash"
        },CancellationToken.None);
        Assert.NotNull(created);
        var before=await repository.GetEditSnapshotAsync(created.OrderId,CancellationToken.None);
        Assert.NotNull(before);
        var originalPiece=Assert.Single(before.Pieces);
        Assert.Equal(6782.50m,originalPiece.OriginalSalePrice);
        Assert.Equal(6782.50m,before.Order.TotalAmount);
        Assert.Equal(782.50m,before.Order.DiscountAmount);
        Assert.Equal(5000m,before.Order.PaidAmount);
        Assert.Equal(1000m,before.Order.RemainingAmount);
        Assert.Equal(100m,await BuyerPointsAsync());
        Assert.Equal(50m,await ReferralNetAsync());
        var paymentCount=await CountAsync(connectionString,"SELECT COUNT(*) FROM dbo.Payments WHERE OrderID=@id",created.OrderId);

        pricing.Price=6782.50m;
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
        Assert.Equal(13565m,added.Order.TotalAmount);
        Assert.Equal(782.50m,added.Order.DiscountAmount);
        Assert.Equal(5000m,added.Order.PaidAmount);
        Assert.Equal(7782.50m,added.Order.RemainingAmount);
        Assert.Equal(2,added.Pieces.Count);
        Assert.Equal(200m,await BuyerPointsAsync());
        Assert.Equal(100m,await ReferralNetAsync());
        Assert.Equal(4m,await AvailableAsync());
        Assert.Equal(paymentCount,await CountAsync(connectionString,"SELECT COUNT(*) FROM dbo.Payments WHERE OrderID=@id",created.OrderId));
        var replay=await repository.ApplyEditAsync(created.OrderId,addRequest with { EditOperationId=Guid.NewGuid() },"Test",CancellationToken.None);
        Assert.Equal(added.Version,replay.Version);
        Assert.Equal(added.AddedPieceIds[addition.AddOperationId],replay.AddedPieceIds[addition.AddOperationId]);
        Assert.Equal(200m,await BuyerPointsAsync());
        Assert.Equal(4m,await AvailableAsync());
        var conflictingAddition=addition with { Measurements=new Dictionary<string,string> { ["Length"]="37" } };
        await Assert.ThrowsAsync<TailoringOrderEditConflictException>(() => repository.ApplyEditAsync(created.OrderId,
            addRequest with { EditOperationId=Guid.NewGuid(), AddedPieces=[conflictingAddition] },"Test",CancellationToken.None));

        var cancelRequest=new TailoringOrderEditRequest
        {
            EditOperationId=Guid.NewGuid(),ExpectedVersion=added.Version,
            CancelledPieces=[new TailoringPieceCancellation { PieceId=originalPiece.PieceId,Reason="Test cancellation" }]
        };
        var cancelled=await repository.ApplyEditAsync(created.OrderId,cancelRequest,"Test",CancellationToken.None);
        Assert.Equal(created.OrderId,cancelled.Order.OrderId);
        Assert.Equal(created.OrderNumber,cancelled.Order.OrderNumber);
        Assert.Equal(6782.50m,cancelled.Order.TotalAmount);
        Assert.Equal(782.50m,cancelled.Order.DiscountAmount);
        Assert.Equal(5000m,cancelled.Order.PaidAmount);
        Assert.Equal(1000m,cancelled.Order.RemainingAmount);
        Assert.Equal("Cancelled",cancelled.Pieces.Single(piece=>piece.PieceId==originalPiece.PieceId).PieceStatus);
        Assert.Equal(2,cancelled.Pieces.Count);
        Assert.Equal(100m,await BuyerPointsAsync());
        Assert.Equal(50m,await ReferralNetAsync());
        Assert.Equal(5m,await AvailableAsync());
        Assert.Equal(0m,await ReadDecimalAsync(connectionString,
            "SELECT COALESCE(SUM(Amount),0) FROM dbo.Payments WHERE OrderID=@id AND PaymentKind=N'Refund'",created.OrderId));
        Assert.Equal(0m,await ReadDecimalAsync(connectionString,
            "SELECT CurrentPoints FROM dbo.TailoringPieceBenefitBalances WHERE PieceId=@id AND BenefitKind=N'Buyer'",originalPiece.PieceId));
        var replayCancel=await repository.ApplyEditAsync(created.OrderId,cancelRequest,"Test",CancellationToken.None);
        Assert.Equal(cancelled.Version,replayCancel.Version);
        Assert.Equal(paymentCount,await CountAsync(connectionString,"SELECT COUNT(*) FROM dbo.Payments WHERE OrderID=@id",created.OrderId));
        Assert.Equal(0m,await ReadDecimalAsync(connectionString,
            "SELECT CurrentPoints FROM dbo.TailoringPieceBenefitBalances WHERE PieceId=@id AND BenefitKind=N'Buyer'",originalPiece.PieceId));
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

    [Fact]
    public async Task TailoringEdit_AddPiece_PreservesOfficialDetailsAndOrderDetailsProjection()
    {
        var connectionString = new SqlConnectionStringBuilder(GetConnectionString())
        {
            InitialCatalog = "LUMAR_TAILORING_EDIT_TEST_20261101"
        }.ConnectionString;
        var seed = await SeedAsync(connectionString);
        var receipt = await ReceiveOrderFabricAsync(connectionString, seed);
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var repository = new OrderRepository(
            new ReadOnlySqlConnectionFactory(options),
            new OperationalSqlConnectionFactory(options),
            new MeasurementConsumptionRules(),
            new EditTestPricing { Price = 20000m });
        var template = await CreateFabricOrderRequestAsync(
            connectionString, seed, $"EDIT-{Guid.NewGuid():N}", 36m, 1);
        var created = await repository.CreateAsync(new CreateOrderDto
        {
            CustomerId = seed.CustomerId,
            RequestReference = template.RequestReference,
            TotalAmount = 20000m,
            DiscountAmount = 2000m,
            SaleCategory = "TailoringOrder",
            Items = template.Items
        }, CancellationToken.None);
        Assert.NotNull(created);

        var before = await repository.GetEditSnapshotAsync(created.OrderId, CancellationToken.None);
        Assert.NotNull(before);
        var originalPiece = Assert.Single(before.Pieces);
        Assert.Equal(20000m, before.Order.TotalAmount);
        Assert.Equal(2000m, before.Order.DiscountAmount);
        Assert.Equal(18000m, before.Order.RemainingAmount);
        var officialProductTypeName = await ReadStringAsync(
            connectionString,
            "SELECT NameAr FROM dbo.PricingProductTypes WHERE ProductTypeId=CONVERT(int,@code)",
            originalPiece.ProductTypeId.ToString());
        Assert.False(string.IsNullOrWhiteSpace(officialProductTypeName));

        var pricing = new EditTestPricing { Price = 15406.25m };
        var addRepository = new OrderRepository(
            new ReadOnlySqlConnectionFactory(options),
            new OperationalSqlConnectionFactory(options),
            new MeasurementConsumptionRules(),
            pricing);
        var addition = new TailoringNewPiece
        {
            AddOperationId = Guid.NewGuid(),
            ProductTypeId = originalPiece.ProductTypeId,
            FabricCode = seed.FabricItemCode,
            FabricType = "الصحراء",
            FabricColor = "كحلي",
            CatalogNumber = "CAT-EDIT-ADD",
            Measurements = new Dictionary<string, string> { ["Length"] = "36" }
        };

        var added = await addRepository.ApplyEditAsync(created.OrderId, new TailoringOrderEditRequest
        {
            EditOperationId = Guid.NewGuid(),
            ExpectedVersion = before.Version,
            AddedPieces = [addition]
        }, "Test", CancellationToken.None);

        Assert.Equal(created.OrderId, added.Order.OrderId);
        Assert.Equal(created.OrderNumber, added.Order.OrderNumber);
        Assert.Equal(seed.CustomerId, added.Order.CustomerId);
        Assert.Equal(35406.25m, added.Order.TotalAmount);
        Assert.Equal(2000m, added.Order.DiscountAmount);
        Assert.Equal(0m, added.Order.PaidAmount);
        Assert.Equal(33406.25m, added.Order.RemainingAmount);

        var pieceId = added.AddedPieceIds[addition.AddOperationId];
        var addedPiece = Assert.Single(added.Pieces, piece => piece.PieceId == pieceId);
        Assert.Equal(originalPiece.ProductTypeId, addedPiece.ProductTypeId);
        Assert.Equal(officialProductTypeName, addedPiece.PieceType);
        Assert.StartsWith("TRK-", addedPiece.TrackingCode);
        Assert.DoesNotContain("ADD-", addedPiece.TrackingCode);
        Assert.Equal(seed.FabricItemCode.ToUpperInvariant(), addedPiece.FabricCode);
        Assert.Equal(addition.FabricType, addedPiece.FabricType);
        Assert.Equal(addition.FabricColor, addedPiece.FabricColor);
        Assert.Equal(addition.CatalogNumber, addedPiece.CatalogNumber);
        Assert.Equal("36", addedPiece.Measurements["Length"]);
        Assert.Equal(15406.25m, addedPiece.OriginalSalePrice);

        var orderDetails = await addRepository.GetByIdAsync(created.OrderId, CancellationToken.None);
        Assert.NotNull(orderDetails);
        Assert.Equal(created.OrderId, orderDetails.OrderId);
        Assert.Equal(created.OrderNumber, orderDetails.OrderNumber);
        Assert.Equal(seed.CustomerId, orderDetails.CustomerId);

        var detailItems = await addRepository.GetItemsAsync(created.OrderId, CancellationToken.None);
        var detailItem = Assert.Single(detailItems, item => item.OrderItemId == addedPiece.OrderItemId);
        Assert.Equal(addedPiece.PieceType, detailItem.PieceType);
        Assert.Equal(addition.FabricCode.ToUpperInvariant(), detailItem.FabricCode);
        Assert.Equal(addition.FabricType, detailItem.FabricType);
        Assert.Equal(addition.FabricColor, detailItem.FabricColor);
        Assert.Equal(originalPiece.ProductTypeId, detailItem.ProductTypeId);
        using (var measurementSnapshot = System.Text.Json.JsonDocument.Parse(detailItem.MeasurementSnapshot!))
        {
            var root = measurementSnapshot.RootElement;
            Assert.Equal(addition.CatalogNumber, root.GetProperty("_catalogNumber").GetString());
            Assert.Equal("36", root.GetProperty("Length").GetString());
            Assert.True(root.GetProperty("_fullCost").GetDecimal() > 0m);
        }

        var detailPieces = await addRepository.GetPiecesAsync(created.OrderId, CancellationToken.None);
        var detailPiece = Assert.Single(detailPieces, piece => piece.PieceId == pieceId);
        Assert.Equal(addedPiece.TrackingCode, detailPiece.TrackingCode);
        var detailFabrics = await addRepository.GetFabricsAsync(created.OrderId, CancellationToken.None);
        var detailFabric = Assert.Single(detailFabrics, fabric => fabric.OrderItemId == addedPiece.OrderItemId);
        Assert.Equal(receipt.InventoryItemId, detailFabric.InventoryItemId);
        Assert.Equal(addition.FabricCode.ToUpperInvariant(), detailFabric.FabricCode);
        Assert.Equal(addition.FabricType, detailFabric.FabricType);
        Assert.Equal(addition.FabricColor, detailFabric.FabricColor);
        Assert.Equal(36m, detailFabric.Quantity);
        Assert.Equal(36m, detailFabric.ConsumedQuantity);

        var yardCost = await ReadDecimalAsync(connectionString,
            "SELECT OfficialUnitCost FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id",
            receipt.InventoryItemId);
        var operationalAmount = await ReadDecimalAsync(connectionString,
            "SELECT OperationalAmount FROM dbo.FabricConsumptionSources WHERE OrderItemId=@id",
            addedPiece.OrderItemId);
        var postingAmount = await ReadDecimalAsync(connectionString,
            "SELECT PostingAmount FROM dbo.FabricConsumptionSources WHERE OrderItemId=@id",
            addedPiece.OrderItemId);
        Assert.Equal(yardCost, operationalAmount);
        Assert.Equal(decimal.Round(operationalAmount, 2, MidpointRounding.AwayFromZero), postingAmount);
        Assert.Equal(operationalAmount, detailFabric.TotalCost);
        Assert.Equal(1, await CountAsync(connectionString,
            "SELECT COUNT(*) FROM dbo.FabricConsumptionSources WHERE OrderItemId=@id",
            addedPiece.OrderItemId));
        Assert.Equal(1, await CountAsync(connectionString,
            "SELECT COUNT(*) FROM dbo.FabricConsumptionSources source JOIN dbo.InventoryTransactions transactionRow ON transactionRow.TransactionID=source.InventoryTransactionId WHERE source.OrderItemId=@id AND transactionRow.TransactionType=N'FabricInventoryConsumed'",
            addedPiece.OrderItemId));

        var pricingSnapshotJson = await ReadStringAsync(
            connectionString,
            "SELECT OriginalPricingSnapshot FROM dbo.TailoringPieceSaleValues WHERE PieceID=CONVERT(int,@code)",
            pieceId.ToString());
        Assert.NotNull(pricingSnapshotJson);
        using var pricingSnapshot = System.Text.Json.JsonDocument.Parse(pricingSnapshotJson!);
        Assert.Equal(originalPiece.ProductTypeId, pricingSnapshot.RootElement.GetProperty("ProductTypeId").GetInt32());
        Assert.Equal(addition.FabricCode.ToUpperInvariant(), pricingSnapshot.RootElement.GetProperty("FabricCode").GetString());
        Assert.Equal(15406.25m, pricingSnapshot.RootElement.GetProperty("FinalPricePerPiece").GetDecimal());
    }

    [Fact]
    public async Task TailoringOrderDetails_ProjectsOfficialFabricCostAndPieceFullCost()
    {
        var connectionString = new SqlConnectionStringBuilder(GetConnectionString())
        {
            InitialCatalog = "LUMAR_TAILORING_EDIT_TEST_20261101"
        }.ConnectionString;
        var seed = await SeedAsync(connectionString);
        await ReceiveOrderFabricAsync(connectionString, seed);
        var options = Options.Create(new DatabaseOptions { ConnectionString = connectionString });
        var repository = new OrderRepository(
            new ReadOnlySqlConnectionFactory(options),
            new OperationalSqlConnectionFactory(options),
            new MeasurementConsumptionRules(),
            new EditTestPricing { Price = 6782.50m, FullCostPerPiece = 3014.44444040m });
        var template = await CreateFabricOrderRequestAsync(
            connectionString, seed, $"EDIT-{Guid.NewGuid():N}", 52m, 1);
        var created = await repository.CreateAsync(new CreateOrderDto
        {
            CustomerId = seed.CustomerId,
            RequestReference = template.RequestReference,
            TotalAmount = 6782.50m,
            Items = template.Items
        }, CancellationToken.None);
        Assert.NotNull(created);

        var item = Assert.Single(await repository.GetItemsAsync(created.OrderId, CancellationToken.None));
        var fabric = Assert.Single(await repository.GetFabricsAsync(created.OrderId, CancellationToken.None));
        var piece = Assert.Single(await repository.GetPiecesAsync(created.OrderId, CancellationToken.None));
        var officialOperationalAmount = await ReadDecimalAsync(
            connectionString,
            "SELECT COALESCE(SUM(OperationalAmount),0) FROM dbo.FabricConsumptionSources WHERE OrderItemId=@id",
            item.OrderItemId);
        var officialFullCost = await ReadDecimalAsync(
            connectionString,
            "SELECT TRY_CONVERT(decimal(18,8),JSON_VALUE(value.OriginalPricingSnapshot,'$.FullCostPerPiece')) FROM dbo.TailoringPieceSaleValues value WHERE value.PieceID=@id",
            piece.PieceId);

        Assert.Equal(52m, fabric.ConsumedQuantity);
        Assert.Equal(decimal.Round(officialOperationalAmount, 2, MidpointRounding.AwayFromZero), fabric.TotalCost);
        Assert.Equal(3014.44444040m, officialFullCost);
        Assert.Equal(officialFullCost, item.FullCost);
        using var measurementSnapshot = System.Text.Json.JsonDocument.Parse(item.MeasurementSnapshot!);
        Assert.False(measurementSnapshot.RootElement.TryGetProperty("_fullCost", out _));
    }

    [Fact]
    public async Task TailoringEdit_SameAddOperationIdWithSamePayload_IsReplaySafe()
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

        var addition=new TailoringNewPiece
        {
            AddOperationId=Guid.NewGuid(),
            ProductTypeId=originalPiece.ProductTypeId,
            FabricCode=seed.FabricItemCode,
            Measurements=new Dictionary<string,string> { ["Length"] = "36" }
        };

        var firstRequest=new TailoringOrderEditRequest
        {
            EditOperationId=Guid.NewGuid(),
            ExpectedVersion=before.Version,
            AddedPieces=[addition]
        };

        var firstResult=await repository.ApplyEditAsync(created.OrderId,firstRequest,"Test",CancellationToken.None);
        Assert.Equal(2,firstResult.Pieces.Count);

        var secondRequest=new TailoringOrderEditRequest
        {
            EditOperationId=Guid.NewGuid(),
            ExpectedVersion=firstResult.Version,
            AddedPieces=[addition with { AddOperationId = addition.AddOperationId }]
        };

        var replay=await repository.ApplyEditAsync(created.OrderId,secondRequest,"Test",CancellationToken.None);
        Assert.Equal(firstResult.Version,replay.Version);
        Assert.Equal(firstResult.AddedPieceIds[addition.AddOperationId],replay.AddedPieceIds[addition.AddOperationId]);
        Assert.Equal(1,await CountAsync(connectionString,"SELECT COUNT(*) FROM dbo.TailoringPieceAddOperations WHERE OrderId=@id",created.OrderId));
        Assert.Equal(2,await CountAsync(connectionString,
            "SELECT COUNT(*) FROM dbo.Pieces piece JOIN dbo.OrderItems item ON item.OrderItemID=piece.OrderItemID WHERE item.OrderID=@id",created.OrderId));
        Assert.Equal(4m,await ReadDecimalAsync(connectionString,
            "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id",receipt.InventoryItemId));
    }

    [Fact]
    public async Task TailoringEdit_ConflictingEditAndAddPayloads_AreRejected()
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

        var editId=Guid.NewGuid();
        var baseAddition=new TailoringNewPiece
        {
            AddOperationId=Guid.NewGuid(),
            ProductTypeId=originalPiece.ProductTypeId,
            FabricCode=seed.FabricItemCode,
            Measurements=new Dictionary<string,string> { ["Length"] = "36" }
        };

        var firstRequest=new TailoringOrderEditRequest
        {
            EditOperationId=editId,
            ExpectedVersion=before.Version,
            AddedPieces=[baseAddition]
        };
        var firstResult=await repository.ApplyEditAsync(created.OrderId,firstRequest,"Test",CancellationToken.None);
        Assert.Equal(2,firstResult.Pieces.Count);

        var sameEditConflict = new TailoringOrderEditRequest
        {
            EditOperationId=editId,
            ExpectedVersion=before.Version,
            AddedPieces=[baseAddition with { Measurements = new Dictionary<string, string> { ["Length"] = "37" } }]
        };
        await Assert.ThrowsAsync<TailoringOrderEditConflictException>(() => repository.ApplyEditAsync(created.OrderId,sameEditConflict,"Test",CancellationToken.None));

        var sameAddConflict = new TailoringOrderEditRequest
        {
            EditOperationId=Guid.NewGuid(),
            ExpectedVersion=firstResult.Version,
            AddedPieces=[baseAddition with { Measurements = new Dictionary<string, string> { ["Length"] = "37" } }]
        };
        await Assert.ThrowsAsync<TailoringOrderEditConflictException>(() => repository.ApplyEditAsync(created.OrderId,sameAddConflict,"Test",CancellationToken.None));
        Assert.Equal(1,await CountAsync(connectionString,
            "SELECT COUNT(*) FROM dbo.TailoringPieceAddOperations WHERE OrderId=@id AND AddOperationId=@addOperationId",
            ("@id", created.OrderId), ("@addOperationId", baseAddition.AddOperationId)));
        Assert.Equal(4m,await ReadDecimalAsync(connectionString,
            "SELECT AvailableQuantity FROM dbo.InventoryItemFoundation WHERE InventoryItemId=@id",receipt.InventoryItemId));
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
        public decimal? FullCostPerPiece { get; set; }
        public Task<PricingEngineResponseDto> CalculateAsync(PricingEngineRequestDto request,CancellationToken cancellationToken)
        {
            var fullCostPerPiece = FullCostPerPiece ?? request.Consumption + 4m;
            return Task.FromResult(new PricingEngineResponseDto(true,[],request.ProductTypeId,request.FabricCode,"Inch",
                request.Consumption,request.Quantity,1m,request.Consumption,request.Consumption*request.Quantity,
                1m,1m,1m,1m,4m,fullCostPerPiece,fullCostPerPiece*request.Quantity,
                0m,0m,request.Consumption+4m,0m,0m,Price,Price*request.Quantity,"Test","Test"));
        }
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