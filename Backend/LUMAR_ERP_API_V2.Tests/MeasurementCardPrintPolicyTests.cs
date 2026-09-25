using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class MeasurementCardPrintPolicyTests
{
    [Fact]
    public void FirstPrint_UsesCopyOneWithoutReason()
    {
        Assert.Equal(1, MeasurementCardPrintPolicy.NextCopyNumber(null, false));
        Assert.Equal(string.Empty, MeasurementCardPrintPolicy.CopyLabel(1));
    }

    [Fact]
    public void LegacyPrintedPiece_StartsAtCopyTwoWithoutCreatingHistory()
    {
        Assert.Equal(2, MeasurementCardPrintPolicy.NextCopyNumber(null, true));
        Assert.Equal("النسخة الثانية", MeasurementCardPrintPolicy.CopyLabel(2));
    }

    [Fact]
    public void CompletedCopies_IncrementFromBackendState()
    {
        Assert.Equal(2, MeasurementCardPrintPolicy.NextCopyNumber(1, false));
        Assert.Equal(3, MeasurementCardPrintPolicy.NextCopyNumber(2, false));
        Assert.Equal(4, MeasurementCardPrintPolicy.NextCopyNumber(3, false));
        Assert.Equal("النسخة الرابعة", MeasurementCardPrintPolicy.CopyLabel(4));
        Assert.Equal("النسخة رقم 5", MeasurementCardPrintPolicy.CopyLabel(5));
    }

    [Fact]
    public void PaymentType_OnlyUsesOfficialCashAndCreditOptions()
    {
        Assert.Equal(MeasurementCardPrintPolicy.Cash, MeasurementCardPrintPolicy.NormalizePaymentType("نقداً"));
        Assert.Equal(MeasurementCardPrintPolicy.Credit, MeasurementCardPrintPolicy.NormalizePaymentType("آجل"));
        Assert.Equal(MeasurementCardPrintPolicy.Donation, MeasurementCardPrintPolicy.NormalizePaymentType("تبرعاً"));
        Assert.Throws<ArgumentException>(() => MeasurementCardPrintPolicy.NormalizePaymentType("غير معروف"));
    }
}
