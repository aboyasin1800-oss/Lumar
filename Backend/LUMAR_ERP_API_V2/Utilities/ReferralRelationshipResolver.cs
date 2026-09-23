namespace LUMAR_ERP_API_V2.Utilities;

public sealed class ReferralTreeNode
{
    public int CustomerId { get; init; }
    public int? ParentCustomerId { get; init; }
    public string? CustomerCode { get; init; }
    public string? CustomerName { get; init; }
    public int Level { get; init; }
    public List<ReferralTreeNode> Children { get; init; } = [];
    public int MaxDepth { get; init; }
}

public sealed class ReferralRelationshipResolver
{
    public bool IsSelfReferral(int referrerCustomerId, int referredCustomerId) => referrerCustomerId == referredCustomerId;

    public bool WouldCreateCycle(IReadOnlyDictionary<int, int?> relationships, int customerId, int candidateParentId)
    {
        if (customerId == candidateParentId) return true;

        var current = candidateParentId;
        var visited = new HashSet<int>();

        while (current != 0)
        {
            if (!visited.Add(current)) return true;

            if (!relationships.TryGetValue(current, out var parent)) break;
            if (parent is null) break;
            if (parent == customerId) return true;
            current = parent.Value;
        }

        return false;
    }

    public ReferralTreeNode BuildTree(int rootCustomerId, IReadOnlyDictionary<int, int?> relationships)
    {
        var children = new Dictionary<int, List<int>>();
        foreach (var pair in relationships)
        {
            if (pair.Value is null) continue;
            if (!children.TryGetValue(pair.Value.Value, out var list))
            {
                list = [];
                children[pair.Value.Value] = list;
            }

            list.Add(pair.Key);
        }

        return BuildNode(rootCustomerId, children, 0, 0, rootCustomerId, relationships);
    }

    private static ReferralTreeNode BuildNode(int customerId, IReadOnlyDictionary<int, List<int>> children, int level, int maxDepth, int rootCustomerId, IReadOnlyDictionary<int, int?> relationships)
    {
        var nodeChildren = children.TryGetValue(customerId, out var list)
            ? list.OrderBy(id => id).Select(child => BuildNode(child, children, level + 1, maxDepth + 1, rootCustomerId, relationships)).ToList()
            : [];

        var deepest = nodeChildren.Count == 0 ? level : nodeChildren.Max(child => child.MaxDepth);

        return new ReferralTreeNode
        {
            CustomerId = customerId,
            ParentCustomerId = relationships.TryGetValue(customerId, out var parent) ? parent : null,
            CustomerCode = customerId.ToString(),
            CustomerName = $"Customer {customerId}",
            Level = level,
            Children = nodeChildren,
            MaxDepth = deepest
        };
    }

    public int CountNodes(ReferralTreeNode root)
    {
        var count = 1;
        foreach (var child in root.Children)
        {
            count += CountNodes(child);
        }

        return count;
    }
}
