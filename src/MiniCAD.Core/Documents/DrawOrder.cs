using MiniCAD.Core.Entities;

namespace MiniCAD.Core.Documents;

/// <summary>
/// Pure paint-order rearrangements (#197). Each method takes the current entity order and the set
/// to move, and returns a new order. "Front" is the end of the list (drawn last/on top), matching
/// both <c>SkiaSceneRenderer</c> and the topmost-first pick. Forward/backward move one step and
/// keep the selected block together.
/// </summary>
public static class DrawOrder
{
    public static IReadOnlyList<IEntity> BringToFront(IReadOnlyList<IEntity> order, IReadOnlyCollection<IEntity> selection)
    {
        var set = AsSet(selection);
        return order.Where(e => !set.Contains(e)).Concat(order.Where(set.Contains)).ToList();
    }

    public static IReadOnlyList<IEntity> SendToBack(IReadOnlyList<IEntity> order, IReadOnlyCollection<IEntity> selection)
    {
        var set = AsSet(selection);
        return order.Where(set.Contains).Concat(order.Where(e => !set.Contains(e))).ToList();
    }

    public static IReadOnlyList<IEntity> BringForward(IReadOnlyList<IEntity> order, IReadOnlyCollection<IEntity> selection)
    {
        var set = AsSet(selection);
        var result = order.ToList();
        // Move each selected item one step toward the end; scan high→low so a block shifts as one.
        for (int i = result.Count - 2; i >= 0; i--)
        {
            if (set.Contains(result[i]) && !set.Contains(result[i + 1]))
                (result[i], result[i + 1]) = (result[i + 1], result[i]);
        }

        return result;
    }

    public static IReadOnlyList<IEntity> SendBackward(IReadOnlyList<IEntity> order, IReadOnlyCollection<IEntity> selection)
    {
        var set = AsSet(selection);
        var result = order.ToList();
        // Move each selected item one step toward the start; scan low→high so a block shifts as one.
        for (int i = 1; i < result.Count; i++)
        {
            if (set.Contains(result[i]) && !set.Contains(result[i - 1]))
                (result[i], result[i - 1]) = (result[i - 1], result[i]);
        }

        return result;
    }

    private static HashSet<IEntity> AsSet(IReadOnlyCollection<IEntity> selection) => new(selection);
}
