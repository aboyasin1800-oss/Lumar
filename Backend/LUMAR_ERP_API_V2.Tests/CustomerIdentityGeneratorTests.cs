using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class CustomerIdentityGeneratorTests
{
    [Fact]
    public void IndependentGenerators_ProduceValidDistinctIdentities()
    {
        var first = new CustomerIdentityGenerator();
        var second = new CustomerIdentityGenerator();
        var codes = new HashSet<string>(StringComparer.Ordinal);
        var identifiers = new HashSet<Guid>();
        for (var sample = 0; sample < 100_000; sample++)
        {
            var generator = sample % 2 == 0 ? first : second;
            var code = generator.GenerateCustomerCode();
            var identifier = generator.GenerateGlobalCustomerUid();
            Assert.Equal(18, code.Length);
            Assert.Matches("^C-[0123456789ABCDEFGHJKMNPQRSTVWXYZ]{5}-[0123456789ABCDEFGHJKMNPQRSTVWXYZ]{5}-[0123456789ABCDEFGHJKMNPQRSTVWXYZ]{4}$", code);
            Assert.True(generator.ValidateNewCustomerCode(code));
            Assert.Equal(7, identifier.Version);
            Assert.Contains(identifier.ToString("D")[19], "89ab");
            Assert.True(codes.Add(code));
            Assert.True(identifiers.Add(identifier));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("C10149")]
    [InlineData("C-A7K3M-9Q8X2-RD4I")]
    [InlineData("C-A7K3M-9Q8X2-RD4L")]
    [InlineData("C-A7K3M-9Q8X2-RD4O")]
    [InlineData("C-A7K3M-9Q8X2-RD4U")]
    [InlineData("c-a7k3m-9q8x2-rd4f")]
    [InlineData(" C-A7K3M-9Q8X2-RD4F")]
    [InlineData("C-A7K3M-9Q8X2-RD4F\n")]
    public void Validation_RejectsNonContractCodes(string? code)
    {
        Assert.False(new CustomerIdentityGenerator().ValidateNewCustomerCode(code));
    }

    [Fact]
    public void Validation_AcceptsDocumentedExample()
    {
        Assert.True(new CustomerIdentityGenerator().ValidateNewCustomerCode("C-A7K3M-9Q8X2-RD4F"));
    }
}