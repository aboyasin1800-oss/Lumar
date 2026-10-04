using LUMAR_ERP_API_V2.DTOs.Inventory;
using LUMAR_ERP_API_V2.Utilities;
using Xunit;

namespace LUMAR_ERP_API_V2.Tests;

public sealed class ToolIssuanceContractsTests
{
    [Fact]
    public void ToolOperationalIssueAccountingType_IsDefined()
    {
        Assert.Equal((byte)14, (byte)AccountingEventType.ToolOperationalIssue);
    }

    [Fact]
    public void ToolOperationalReversalAccountingType_IsDefined()
    {
        Assert.Equal((byte)15, (byte)AccountingEventType.ToolOperationalReversal);
    }

    [Fact]
    public void ToolIssuanceDtos_AreAvailable()
    {
        var issue = new CreateToolOperationalIssueDto
        {
            InventoryItemId = 1,
            Quantity = 2m,
            OfficialUnitCost = 50m,
            OperationalReason = "Issue for project"
        };

        var custody = new CreateToolCustodyIssueDto
        {
            InventoryItemId = 1,
            Quantity = 2m,
            OfficialUnitCost = 50m,
            BeneficiaryName = "Ali",
            DestinationType = "Project",
            DestinationName = "Site A",
            LoanReason = "Use"
        };

        Assert.Equal(1, issue.InventoryItemId);
        Assert.Equal("Ali", custody.BeneficiaryName);
    }
}
