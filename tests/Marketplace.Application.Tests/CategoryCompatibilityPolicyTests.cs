using Marketplace.Application.Categories;

namespace Marketplace.Application.Tests;

public sealed class CategoryCompatibilityPolicyTests
{
    [Fact]
    public void SelectingAParentMatchesItsChildrenAndSelectingAChildMatchesItsParents()
    {
        Guid root = Guid.NewGuid();
        Guid child = Guid.NewGuid();
        Guid grandchild = Guid.NewGuid();
        Dictionary<Guid, Guid?> parents = new()
        {
            [root] = null,
            [child] = root,
            [grandchild] = child
        };

        HashSet<Guid> fromParent = CategoryCompatibilityPolicy.Expand([root], parents);
        HashSet<Guid> fromGrandchild = CategoryCompatibilityPolicy.Expand([grandchild], parents);

        Assert.Equal(3, fromParent.Count);
        Assert.Contains(grandchild, fromParent);
        Assert.Contains(root, fromGrandchild);
        Assert.Contains(child, fromGrandchild);
    }
}
