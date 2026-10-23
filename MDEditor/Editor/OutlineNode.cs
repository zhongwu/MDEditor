using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MDEditor.Core.Models;

namespace MDEditor.Editor;

/// <summary>
/// Bindable wrapper around <see cref="OutlineItem"/> for the outline TreeView.
/// Holds UI-only state (expanded, current section).
/// </summary>
public sealed partial class OutlineNode : ObservableObject
{
    public OutlineNode(OutlineItem item, OutlineNode? parent)
    {
        Level = item.Level;
        Title = item.Title;
        Line = item.Line;
        Id = item.Id;
        Parent = parent;
        Key = parent == null ? Title : parent.Key + "\u001f" + Title;

        foreach (var child in item.Children)
            Children.Add(new OutlineNode(child, this));
    }

    public int Level { get; }
    public string Title { get; }
    public int Line { get; }
    public string? Id { get; }
    public OutlineNode? Parent { get; }

    /// <summary>Path of titles from the root; used to keep collapse state across rebuilds.</summary>
    public string Key { get; }

    public ObservableCollection<OutlineNode> Children { get; } = new();

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isCurrent;

    /// <summary>Builds bindable nodes, collapsing any node whose key is in <paramref name="collapsedKeys"/>.</summary>
    public static List<OutlineNode> Build(IEnumerable<OutlineItem> items, ISet<string> collapsedKeys)
    {
        var roots = items.Select(i => new OutlineNode(i, null)).ToList();
        foreach (var node in Flatten(roots))
        {
            if (collapsedKeys.Contains(node.Key))
                node.IsExpanded = false;
        }

        return roots;
    }

    /// <summary>All nodes in document order (pre-order).</summary>
    public static IEnumerable<OutlineNode> Flatten(IEnumerable<OutlineNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
                yield return child;
        }
    }
}
