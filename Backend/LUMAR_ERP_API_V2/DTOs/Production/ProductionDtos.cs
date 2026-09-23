using System.ComponentModel.DataAnnotations;

namespace LUMAR_ERP_API_V2.DTOs.Production;

public sealed record PieceDto(int PieceId, int OrderItemId, string TrackingCode, string PieceStatus, int PieceNumber, DateTime CreatedDate, string PieceType, int OrderId, string OrderNumber, int? ProductTypeId);
public sealed record WorkCardDto(int PieceId, int OrderItemId, int OrderId, string OrderNumber, int PieceNumber, string TrackingCode, string? CustomerCode, string? CustomerName, string? PhoneNumber, string PieceType, int Quantity, string? FabricType, string? FabricColor, string? FabricCode, string? CatalogNumber, string? Request1, string? Request2, string? Notes1, string? Notes2, string? SpecialRequest, string? MeasurementSnapshot, DateTime? DeliveryDate, string PieceStatus, IReadOnlyList<TrackingEventDto> TrackingHistory);
public sealed record ProductionDashboardDto(int TotalPieces, int InProductionPieces, int ReadyPieces, int DeliveredPieces, int ActiveStages, int ReadyForSaleProducts, int DelayedPieces);
public sealed record FactoryMonitoringDashboardDto(int TotalAtRisk, int TotalStalled, int TotalBlocked, int TotalReadyForDelivery, decimal OverallProgressPercent, DateTime LastUpdatedAt, IReadOnlyList<FactoryMonitoringOrderDto> AtRiskOrders, IReadOnlyList<FactoryMonitoringOrderDto> StalledOrders, IReadOnlyList<FactoryMonitoringOrderDto> BlockedOrders, IReadOnlyList<FactoryMonitoringOrderDto> ReadyForDeliveryOrders);
public sealed record FactoryMonitoringOrderDto(int OrderId, string OrderNumber, string CustomerCode, string CustomerName, DateTime? OrderDate, DateTime? DeliveryDate, int DaysRemaining, int TotalPieces, int CompletedPieces, int IncompletePieces, int ProgressPercent, string Classification, string Reason, string? DelayedPieceCode, string? DelayedPieceType, string? DelayedCurrentStage, string? DelayedNextStage, string? LastEmployeeCode, DateTime? LastUpdatedAt, string? LastStage);
public sealed record FactoryMonitoringClassificationResult(string Classification, string Reason);
public sealed record FactoryMonitoringPieceSnapshot(int PieceId, string PieceType, string TrackingCode, string CurrentStage, string? NextStage, DateTime? LastTrackingEventAt, string? LastEmployeeCode, decimal ProgressPercent, bool IsCompleted);
public sealed record ProductionTrackingAdvanceRequestDto(int? PieceId, string? TrackingCode, string PieceType, string RequestedStage, string? ScannerCode, string? EmployeeCode, string? OperationReference, int ProductTypeId);
public sealed record ProductionTrackingAdvanceResultDto(int? PieceId, string? TrackingCode, string PreviousStatus, string NewStatus, string? NextStage, string Message, bool Updated);
public sealed record ProductionTrackingRouteDto(int ProductTypeId, string ProductTypeCode, string ProductTypeNameAr, string PieceType, IReadOnlyList<string> Route, string CurrentStage, string? NextStage);
public sealed record ReadyMadeProductionOrderDto(int ReadyMadeProductionOrderId, string ProductionOrderNumber, string ProductionName, decimal TotalCost, decimal ProfitPercentage, decimal SuggestedSellingPrice, string Status, string? Notes, DateTime CreatedAt);
public sealed record ReadyMadeProductionOrderItemDto(int ReadyMadeProductionOrderItemId, int ReadyMadeProductionOrderId, int ProductTypeId, string PieceType, int Quantity, string? FabricCode, string? FabricType, string? FabricColor, string? CatalogNumber, decimal? FabricCost, decimal? PieceCost, decimal? LineTotal, string? MeasurementSnapshot, string PieceStatus, DateTime CreatedAt);
public sealed record ReadyMadeProductionPieceDto(int ReadyMadeProductionOrderPieceInstanceId, int ReadyMadeProductionOrderItemId, int PieceNumber, string TrackingCode, string PieceStatus, DateTime CreatedAt);
public sealed record ReadyMadeProductionOrderCreateItemDto(string PieceType, int Quantity, int ProductTypeId, string? FabricCode, string? FabricType, string? FabricColor, string? CatalogNumber, decimal? FabricCost, decimal? PieceCost, decimal? LineTotal, string? MeasurementSnapshot, string? Notes);
public sealed record ReadyMadeProductionOrderCreateDto(string ProductionOrderNumber, string ProductionName, decimal TotalCost, decimal ProfitPercentage, decimal SuggestedSellingPrice, string? Notes, IReadOnlyList<ReadyMadeProductionOrderCreateItemDto> Items);
public sealed record ReadyMadeProductionOrderCreateResultDto(int ReadyMadeProductionOrderId, string ProductionOrderNumber, string ProductionName, decimal TotalCost, decimal ProfitPercentage, decimal SuggestedSellingPrice, string Status, DateTime CreatedAt);
public sealed record ProductionDeliveryDto(int OrderId, string OrderNumber, int CustomerId, string? CustomerCode, string? CustomerName, string? PhoneNumber, string OrderStatus, DateTime? DeliveryDate);
public sealed record PieceTrackingDto(int TrackingCode, int? InvoiceId, int? CustomerId, string? ItemType, string? Status, string? CuttingEmployee, string? SewingEmployee, string? IroningEmployee, DateTime? CreatedDate, DateTime? CuttingDate, DateTime? SewingDate, DateTime? IroningDate, bool? IsCompleted, bool? IsDelivered, DateTime? DeliveryDate, bool? IsOnHold, string? HoldReason);
public sealed record ProductionStageDto(string Stage, string Status);
public sealed record ScannerDto(int ScannerId, string ScannerCode, string ScannerName, string? Description, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);
public sealed record CreateScannerDto
{
    [Required, StringLength(50)] public string? ScannerCode { get; init; }
    [Required, StringLength(200)] public string? ScannerName { get; init; }
    [StringLength(500)] public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
}
public sealed record UpdateScannerDto
{
    [Required, StringLength(50)] public string? ScannerCode { get; init; }
    [Required, StringLength(200)] public string? ScannerName { get; init; }
    [StringLength(500)] public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
}
public sealed record LiveScanDto(int Id, int? TrackingCode, DateTime? ScanTime);
public sealed record TrackingEventDto(int TrackingEventId, int? OrderItemId, int? OrderId, string? TrackingCode, string Stage, string Status, DateTime EventTime, string? EmployeeCode, string? Notes, bool IsReverted, DateTime? RevertedAt, int? PieceId, int? ReadyMadeProductionOrderPieceInstanceId);
public sealed record PieceWageDto(int PieceWageRecordId, int OrderId, int OrderItemId, int PieceId, int TrackingEventId, int? EmployeeId, string? EmployeeCode, string PieceType, string Stage, decimal Quantity, decimal WageRate, decimal TotalWage, int? PayrollPeriodId, int? PayrollRecordId, string Status, string? Notes, DateTime CreatedAt);
public sealed record PieceWageRateDto(int PieceWageRateId, string PieceType, string Stage, decimal WageRate, bool IsActive, string? Notes, DateTime CreatedAt, DateTime? UpdatedAt);