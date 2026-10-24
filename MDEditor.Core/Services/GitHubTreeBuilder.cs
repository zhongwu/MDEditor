using MDEditor.Core.Models;

namespace MDEditor.Core.Services;

/// <summary>
/// A folder or file node in a repository tree built by <see cref="GitHubTreeBuilder"/>.
/// </summary>
public class GitHubTreeNode
{
    public string Name { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public bool IsFolder { get; init; }
    public List<GitHubTreeNode> Children { get; } = new();
}

/// <summary>
/// Turns GitHub's flat recursive tree listing into a folder hierarchy.
/// </summary>
public static class GitHubTreeBuilder
{
    /// <summary>
    /// Builds the top-level nodes, folders first then files, each alphabetically.
    /// </summary>
    /// <param name="markdownOnly">Keep only Markdown files, and only folders that (transitively) contain one.</param>
    public static List<GitHubTreeNode> Build(IEnumerable<GitHubTreeEntry> entries, bool markdownOnly)
    {
        var root = new GitHubTreeNode { IsFolder = true };
        var folders = new Dictionary<string, GitHubTreeNode>(StringComparer.Ordinal) { [string.Empty] = root };

        // Ordinal path order guarantees a folder is listed before its contents.
        foreach (var entry in entries.OrderBy(e => e.Path, StringComparer.Ordinal))
        {
            if (!entry.IsFolder && markdownOnly && !GitHubRepositoryService.IsMarkdownPath(entry.Path))
                continue;

            var parent = GetOrCreateFolder(folders, ParentPath(entry.Path));
            if (entry.IsFolder)
            {
                if (!folders.ContainsKey(entry.Path))
                {
                    var folder = new GitHubTreeNode { Name = entry.Name, Path = entry.Path, IsFolder = true };
                    folders[entry.Path] = folder;
                    parent.Children.Add(folder);
                }
            }
            else
            {
                parent.Children.Add(new GitHubTreeNode { Name = entry.Name, Path = entry.Path });
            }
        }

        if (markdownOnly)
            Prune(root);

        Sort(root);
        return root.Children;
    }

    private static GitHubTreeNode GetOrCreateFolder(Dictionary<string, GitHubTreeNode> folders, string path)
    {
        if (folders.TryGetValue(path, out var existing))
            return existing;

        // A truncated listing can include files whose folder entry was cut off.
        var parent = GetOrCreateFolder(folders, ParentPath(path));
        var folder = new GitHubTreeNode { Name = path[(path.LastIndexOf('/') + 1)..], Path = path, IsFolder = true };
        folders[path] = folder;
        parent.Children.Add(folder);
        return folder;
    }

    private static string ParentPath(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? string.Empty : path[..slash];
    }

    /// <summary>Removes folders with no files beneath them; returns true if the node is now empty.</summary>
    private static bool Prune(GitHubTreeNode node)
    {
        node.Children.RemoveAll(child => child.IsFolder && Prune(child));
        return node.Children.Count == 0;
    }

    private static void Sort(GitHubTreeNode node)
    {
        node.Children.Sort((a, b) => a.IsFolder != b.IsFolder
            ? (a.IsFolder ? -1 : 1)
            : StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));
        foreach (var child in node.Children)
            Sort(child);
    }
}
