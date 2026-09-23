using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public class OrderPaymentFinancialMovementResolverTests
{
    [Theory]
    [InlineData("Advance", "CustomerAdvance", "Advance payment for order")]
    [InlineData("DebtCollection", "CustomerPayment", "Customer payment for order")]
    public void ShouldResolveExpectedTransactionMetadata(string paymentKind, string expectedType, string expectedDescription)
    {
        var result = OrderPaymentFinancialMovementResolver.Resolve(paymentKind);

        Assert.Equal(expectedType, result.TransactionType);
        Assert.Equal(expectedDescription, result.Description);
    }
}
