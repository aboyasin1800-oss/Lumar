namespace LUMAR_ERP_API_V2.Utilities;

public sealed record CustomerIdentity(
    int CustomerId,
    string? CustomerCode,
    string? CustomerName,
    bool IsActive = true,
    string? ReferralCode = null);

public sealed record ReferralRegistrationEdge(int ReferrerCustomerId, int ReferredCustomerId);

public sealed record ReferralTreeBuildResult(
    int RootCustomerId,
    ReferralTreeNodeData Root,
    IReadOnlyList<ReferralTreeNodeData> Children,
    int TotalDescendantsCount,
    int MaxDepth);

public sealed record ReferralTreeNodeData(
    int CustomerId,
    string? CustomerCode,
    string? CustomerName,
    int? ParentCustomerId,
    int Level,
    string? ReferralCode,
    bool IsActive,
    IReadOnlyList<ReferralTreeNodeData> Children,
    int DirectChildrenCount,
    int TotalDescendantsCount,
    int MaxDepth);

public static class ReferralTreeBuilder
{
    public static IReadOnlyList<int> DiscoverRootCustomerIds(IEnumerable<ReferralRegistrationEdge> registrations)
    {
        var referrers = registrations.Select(edge => edge.ReferrerCustomerId).ToHashSet();
        var referred = registrations.Select(edge => edge.ReferredCustomerId).ToHashSet();

        return referrers
            .Except(referred)
            .OrderBy(id => id)
            .ToList();
    }

    public static ReferralTreeBuildResult Build(
        int rootCustomerId,
        IEnumerable<ReferralRegistrationEdge> registrations,
        IReadOnlyDictionary<int, CustomerIdentity> customers)
    {
        if (!customers.ContainsKey(rootCustomerId))
        {
            throw new InvalidOperationException($"Customer {rootCustomerId} not found.");
        }

        var parentLookup = registrations
            .Where(edge => edge.ReferredCustomerId != edge.ReferrerCustomerId)
            .GroupBy(edge => edge.ReferredCustomerId)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.ReferrerCustomerId).First());

        var childrenByParent = registrations
            .GroupBy(edge => edge.ReferrerCustomerId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(edge => edge.ReferredCustomerId)
                    .Distinct()
                    .OrderBy(id => id)
                    .ToList());

        var root = BuildNode(rootCustomerId, 0, null, childrenByParent, parentLookup, customers);
        return new ReferralTreeBuildResult(
            rootCustomerId,
            root,
            root.Children,
            root.TotalDescendantsCount,
            root.MaxDepth);
    }

    private static ReferralTreeNodeData BuildNode(
        int customerId,
        int level,
        int? parentCustomerId,
        IReadOnlyDictionary<int, List<int>> childrenByParent,
        IReadOnlyDictionary<int, int> parentLookup,
        IReadOnlyDictionary<int, CustomerIdentity> customers)
    {
        var directParent = parentCustomerId ?? (parentLookup.TryGetValue(customerId, out var parent) ? parent : null);

        if (!customers.TryGetValue(customerId, out var customer))
        {
            var children = childrenByParent.TryGetValue(customerId, out var childIds)
                ? childIds
                    .Select(id => BuildNode(id, level + 1, customerId, childrenByParent, parentLookup, customers))
                    .ToList()
                : [];

            var totalDescendants = children.Sum(child => 1 + child.TotalDescendantsCount);
            var fallbackDepth = children.Count == 0 ? level : Math.Max(level, children.Max(child => child.MaxDepth));

            return new ReferralTreeNodeData(
                customerId,
                null,
                $"عميل {customerId}",
                directParent,
                level,
                null,
                false,
                children,
                children.Count,
                totalDescendants,
                fallbackDepth);
        }

        var childNodes = childrenByParent.TryGetValue(customerId, out var currentChildren)
            ? currentChildren
                .Select(id => BuildNode(id, level + 1, customerId, childrenByParent, parentLookup, customers))
                .ToList()
            : [];

        var directChildrenCount = childNodes.Count;
        var totalDescendantsCount = childNodes.Sum(child => 1 + child.TotalDescendantsCount);
        var computedDepth = childNodes.Count == 0 ? level : Math.Max(level, childNodes.Max(child => child.MaxDepth));

        return new ReferralTreeNodeData(
            customer.CustomerId,
            customer.CustomerCode,
            customer.CustomerName,
            directParent,
            level,
            customer.ReferralCode,
            customer.IsActive,
            childNodes,
            directChildrenCount,
            totalDescendantsCount,
            computedDepth);
    }
}
