using LUMAR_ERP_API_V2.DTOs.Finance;
using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class GeneralCashAccountSettingValidatorTests
{
    [Fact]
    public void IsEligible_AllowsOnlyActiveYerCashAccountsThatAcceptReceiptsAndDisbursements()
    {
        var account = new CashAccountDto(
            CashAccountId: 42,
            AccountName: "Petty Cash 0719001842",
            DerivedBalance: 0m,
            HistoricalSnapshotBalance: 0m,
            IsActive: true,
            IsReceiptEnabled: true,
            IsDisbursementEnabled: true,
            CurrencyCode: "YER",
            CreatedAt: DateTime.UtcNow);

        Assert.True(GeneralCashAccountSettingValidator.IsEligible(account));
    }

    [Fact]
    public void IsEligible_RejectsInactiveOrNonYerOrRestrictedAccounts()
    {
        var invalid = new CashAccountDto(
            CashAccountId: 43,
            AccountName: "USD Cash",
            DerivedBalance: 0m,
            HistoricalSnapshotBalance: 0m,
            IsActive: true,
            IsReceiptEnabled: true,
            IsDisbursementEnabled: true,
            CurrencyCode: "USD",
            CreatedAt: DateTime.UtcNow);

        Assert.False(GeneralCashAccountSettingValidator.IsEligible(invalid));
    }
}
