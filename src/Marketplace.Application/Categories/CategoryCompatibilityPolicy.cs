namespace Marketplace.Application.Categories;

public static class CategoryCompatibilityPolicy
{
    public static HashSet<Guid> Expand(
        IEnumerable<Guid> selectedCategoryIds,
        IReadOnlyDictionary<Guid, Guid?> categoryParents)
    {
        HashSet<Guid> selected = selectedCategoryIds.ToHashSet();
        HashSet<Guid> compatible = [.. selected];

        foreach (Guid categoryId in selected)
        {
            Guid current = categoryId;
            HashSet<Guid> visited = [];
            while (visited.Add(current)
                   && categoryParents.TryGetValue(current, out Guid? parentId)
                   && parentId.HasValue)
            {
                compatible.Add(parentId.Value);
                current = parentId.Value;
            }
        }

        foreach (Guid candidateId in categoryParents.Keys)
        {
            Guid current = candidateId;
            HashSet<Guid> visited = [];
            while (visited.Add(current)
                   && categoryParents.TryGetValue(current, out Guid? parentId)
                   && parentId.HasValue)
            {
                if (selected.Contains(parentId.Value))
                {
                    compatible.Add(candidateId);
                    break;
                }
                current = parentId.Value;
            }
        }

        return compatible;
    }
}
