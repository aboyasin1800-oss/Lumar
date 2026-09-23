using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class ReferralPhaseOneTests
{
    [Fact]
    public void IsSelfReferral_ReturnsTrue_WhenReferrerEqualsReferred()
    {
        var resolver = new ReferralRelationshipResolver();

        var result = resolver.IsSelfReferral(10, 10);

        Assert.True(result);
    }

    [Fact]
    public void WouldCreateCycle_ReturnsTrue_WhenCandidateParentAlreadyDescendsFromChild()
    {
        var resolver = new ReferralRelationshipResolver();
        var relationships = new Dictionary<int, int?>
        {
            [50] = 51,
            [51] = 52,
            [52] = 53,
            [53] = 54
        };

        var result = resolver.WouldCreateCycle(relationships, 54, 50);

        Assert.True(result);
    }

    [Fact]
    public void BuildTree_CreatesCorrectDepthForReferralChain()
    {
        var resolver = new ReferralRelationshipResolver();
        var relationships = new Dictionary<int, int?>
        {
            [50] = null,
            [51] = 50,
            [52] = 51,
            [53] = 52,
            [54] = 53
        };

        var root = resolver.BuildTree(50, relationships);

        Assert.Equal(50, root.CustomerId);
        Assert.Equal(5, resolver.CountNodes(root));
        Assert.Equal(4, root.MaxDepth);
    }
}
