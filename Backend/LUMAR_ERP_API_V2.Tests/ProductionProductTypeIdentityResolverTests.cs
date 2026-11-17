using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class ProductionProductTypeIdentityResolverTests
{
    [Theory]
    [InlineData("Garment", "Both", true)]
    [InlineData("Garment", "Tailoring", true)]
    [InlineData("Garment", "Other", false)]
    [InlineData("Validation", "Both", false)]
    [InlineData("Validation", "Tailoring", false)]
    [InlineData(null, "Both", false)]
    [InlineData("Garment", null, false)]
    public void IsOfficialProductType_Uses_Official_Garment_And_Tailoring_Filter(string? category, string? scope, bool expected)
    {
        Assert.Equal(expected, ProductionProductTypeIdentityResolver.IsOfficialProductType(category, scope));
    }
}
