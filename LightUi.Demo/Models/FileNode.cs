namespace LightUi.Demo.Models;

/// <summary>TreeView 演示用的文件节点：名称 + 图标码点 + 子节点。</summary>
internal sealed class FileNode(string name, string glyph, bool isExpanded = false, params FileNode[] children)
{
    public string Name { get; } = name;

    /// <summary>Segoe Fluent Icons / Segoe MDL2 Assets 的码点。</summary>
    public string Glyph { get; } = glyph;

    /// <summary>初始是否展开；绑在 TreeView.ItemContainerStyle 上。</summary>
    public bool IsExpanded { get; set; } = isExpanded;

    public IReadOnlyList<FileNode> Children { get; } = children;
}
