using LUMAR_ERP_API_V2.Configuration;
using LUMAR_ERP_API_V2.Authorization;
using LUMAR_ERP_API_V2.Data;
using LUMAR_ERP_API_V2.ErrorHandling;
using LUMAR_ERP_API_V2.FinancialFoundation;
using LUMAR_ERP_API_V2.Repositories;
using LUMAR_ERP_API_V2.Services;
using LUMAR_ERP_API_V2.Utilities;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<GlobalApiErrorResultFilter>());
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.CustomSchemaIds(type => type.FullName!));
builder.Services.Configure<DatabaseOptions>(builder.Configuration.GetSection(DatabaseOptions.SectionName));
builder.Services.Configure<Es7OperationalTestModeOptions>(builder.Configuration.GetSection(Es7OperationalTestModeOptions.SectionName));
builder.Services.Configure<WhatsAppOptions>(builder.Configuration.GetSection(WhatsAppOptions.SectionName));
builder.Services.Configure<FirebasePushOptions>(options =>
{
    var config = builder.Configuration.GetSection(FirebasePushOptions.SectionName);
    config.Bind(options);

    var projectId = Environment.GetEnvironmentVariable("LUMAR_FIREBASE_PROJECT_ID");
    var serviceAccountPath = Environment.GetEnvironmentVariable("LUMAR_FIREBASE_SERVICE_ACCOUNT_PATH")
        ?? Environment.GetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS");

    options.Enabled = (config.GetValue<bool?>("Enabled") ?? false)
        || !string.IsNullOrWhiteSpace(projectId)
        || !string.IsNullOrWhiteSpace(serviceAccountPath);
    options.ProjectId = string.IsNullOrWhiteSpace(projectId) ? config["ProjectId"] ?? string.Empty : projectId;
    options.ServiceAccountJsonPath = string.IsNullOrWhiteSpace(serviceAccountPath) ? config["ServiceAccountJsonPath"] ?? string.Empty : serviceAccountPath;
    options.OAuthTokenUrl = string.IsNullOrWhiteSpace(config["OAuthTokenUrl"]) ? options.OAuthTokenUrl : config["OAuthTokenUrl"]!;
    options.FcmSendUrl = string.IsNullOrWhiteSpace(config["FcmSendUrl"]) ? options.FcmSendUrl : config["FcmSendUrl"]!;
    options.TimeoutSeconds = config.GetValue<int?>("TimeoutSeconds") ?? options.TimeoutSeconds;
});

var dataProtectionKeysPath = Environment.GetEnvironmentVariable("LUMAR_ERP_API_V2_DATA_PROTECTION_KEYS_PATH");
var resolvedKeysPath = string.IsNullOrWhiteSpace(dataProtectionKeysPath)
    ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LUMAR_ERP_API_V2", "DataProtection-Keys")
    : dataProtectionKeysPath;

builder.Services.AddDataProtection()
    .SetApplicationName("LUMAR_ERP_API_V2")
    .PersistKeysToFileSystem(new DirectoryInfo(resolvedKeysPath));

builder.Services.AddSingleton<Es7OperationalTestMode>();
builder.Services.AddSingleton<ReadOnlySqlConnectionFactory>();
builder.Services.AddSingleton<OperationalSqlConnectionFactory>();
builder.Services.AddScoped<IProductionProductTypeIdentityResolver, ProductionProductTypeIdentityResolver>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IReferralRepository, ReferralRepository>();
builder.Services.AddScoped<ILoyaltyRepository, LoyaltyRepository>();
builder.Services.AddScoped<IVipLevelEvaluationRepository, VipLevelEvaluationRepository>();
builder.Services.AddScoped<IPiecePointSettingsRepository, PiecePointSettingsRepository>();
builder.Services.AddScoped<ILoyaltyManagementSettingsRepository, LoyaltyManagementSettingsRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReadyMadeSalesRepository, ReadyMadeSalesRepository>();
builder.Services.AddScoped<IProductionRepository, ProductionRepository>();
builder.Services.AddScoped<InventoryRepository>();
builder.Services.AddScoped<IInventoryRepository>(services => services.GetRequiredService<InventoryRepository>());
builder.Services.AddScoped<IGoodsReceiptTransactionRuntime>(services => services.GetRequiredService<InventoryRepository>());
builder.Services.AddScoped<IScannerRepository, ScannerRepository>();
builder.Services.AddScoped<IPieceWageRepository, PieceWageRepository>();
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IPayrollRepository, PayrollRepository>();
builder.Services.AddScoped<IFinanceRepository, FinanceRepository>();
builder.Services.AddScoped<ISupplierRepository, SupplierRepository>();
builder.Services.AddScoped<ISupplierMessagingRepository, SupplierMessagingRepository>();
builder.Services.AddScoped<ISupplierPaymentResponseRepository, SupplierPaymentResponseRepository>();
builder.Services.AddScoped<IPurchasingRepository, PurchasingRepository>();
builder.Services.AddScoped<ISupplierInvoiceLineRepository, SupplierInvoiceLineRepository>();
builder.Services.AddScoped<IMessageRepository, MessageRepository>();
builder.Services.AddScoped<IMessageWriteService, MessageWriteService>();
builder.Services.AddScoped<IYemenPhoneNumberNormalizer, YemenPhoneNumberNormalizer>();
builder.Services.AddScoped<IManualCampaignService, ManualCampaignService>();
builder.Services.AddScoped<IMessageTemplateRenderer, MessageTemplateRenderer>();
builder.Services.AddScoped<ISettingsRepository, SettingsRepository>();
builder.Services.AddScoped<IPrintingRepository, PrintingRepository>();
builder.Services.AddScoped<ICodePrefixSettingsRepository, CodePrefixSettingsRepository>();
builder.Services.AddScoped<IPieceCostSettingRepository, PieceCostSettingRepository>();
builder.Services.AddScoped<IPieceCostManagementRepository, PieceCostManagementRepository>();
builder.Services.AddScoped<IPricingProfitSettingsRepository, PricingProfitSettingsRepository>();
builder.Services.AddScoped<IConsumptionRulesRepository, ConsumptionRulesRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<IMobilePushDeviceRepository, MobilePushDeviceRepository>();
builder.Services.AddScoped<IPushDeviceService, PushDeviceService>();
builder.Services.AddScoped<IPushSender, FirebasePushSender>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IReferralService, ReferralService>();
builder.Services.AddScoped<ILoyaltyService, LoyaltyService>();
builder.Services.AddScoped<ILoyaltyAccountLifecycleService, LoyaltyAccountLifecycleService>();
builder.Services.AddScoped<IVipLevelEvaluationService, VipLevelEvaluationService>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<LoyaltyFreezeBackgroundService>();
builder.Services.AddScoped<ILoyaltyRedemptionRepository, LoyaltyRedemptionRepository>();
builder.Services.AddScoped<ILoyaltyRedemptionService, LoyaltyRedemptionService>();
builder.Services.AddScoped<ILoyaltyManagementSettingsService, LoyaltyManagementSettingsService>();
builder.Services.AddScoped<IPiecePointSettingsService, PiecePointSettingsService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReadyMadeSalesService, ReadyMadeSalesService>();
builder.Services.AddScoped<IProductionService, ProductionService>();
builder.Services.AddScoped<IProductionTrackingService, ProductionTrackingService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IScannerService, ScannerService>();
builder.Services.AddScoped<IPieceWageService, PieceWageService>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IPayrollService, PayrollService>();
builder.Services.AddScoped<IFinanceService, FinanceService>();
builder.Services.AddScoped<ISupplierService, SupplierService>();
builder.Services.AddScoped<ISupplierMessagingService, SupplierMessagingService>();
builder.Services.AddScoped<ISupplierPaymentResponseService, SupplierPaymentResponseService>();
builder.Services.AddScoped<IPurchasingService, PurchasingService>();
builder.Services.AddScoped<ISupplierInvoiceLineService, SupplierInvoiceLineService>();
builder.Services.AddScoped<IMessageService, MessageService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<IWhatsAppConnectionSettingsStore, WhatsAppConnectionSettingsStore>();
builder.Services.AddScoped<IPrintingService, PrintingService>();
builder.Services.AddScoped<ICodePrefixSettingsService, CodePrefixSettingsService>();
builder.Services.AddScoped<IPieceCostSettingService, PieceCostSettingService>();
builder.Services.AddScoped<IPieceCostManagementService, PieceCostManagementService>();
builder.Services.AddScoped<IPricingProfitSettingsService, PricingProfitSettingsService>();
builder.Services.AddScoped<IPricingEngineService, PricingEngineService>();
builder.Services.AddScoped<IConsumptionRulesService, ConsumptionRulesService>();
builder.Services.AddScoped<ISalesReferenceService, SalesReferenceService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAuthenticatedUserContext, AuthenticatedUserContext>();
builder.Services.AddScoped<ISupplierOwnershipResolver, SupplierOwnershipResolver>();
builder.Services.AddScoped<SupplierFinancialRuntime>();
builder.Services.AddScoped<Es7OperationalAudit>();
builder.Services.AddScoped<ISupplierFinancialWorkflowCoordinator, SupplierFinancialWorkflowCoordinator>();
builder.Services.AddScoped<IGoodsReceiptWorkflowCoordinator, GoodsReceiptWorkflowCoordinator>();
builder.Services.AddScoped<IOrderLoyaltyIntegrationService, OrderLoyaltyIntegrationService>();
builder.Services.AddHostedService<OutboundMessageBackgroundService>();
builder.Services.AddScoped<IOutboundMessageDispatcher, OutboundMessageDispatcher>();
builder.Services.AddScoped<IOutboundMessageChannel, InAppMessageChannel>();
builder.Services.AddScoped<IOutboundMessageChannel, WhatsAppCloudMessageChannel>();
builder.Services.AddScoped<ICancelledPieceReadyInventoryTransferService, CancelledPieceReadyInventoryTransferService>();
builder.Services.AddScoped<ICancelledPieceDispositionRepository, CancelledPieceDispositionRepository>();
builder.Services.AddScoped<ICancelledPieceDispositionService, CancelledPieceDispositionService>();

var app = builder.Build();

app.UseRouting();
app.UseMiddleware<GlobalExceptionHandlingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

await app.RunAsync();
