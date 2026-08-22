namespace Hyakkei.Core;

/// <summary>工具模块注册表。核心不依赖任何具体工具，由宿主在启动时注册。</summary>
public sealed class ToolRegistry
{
    private readonly List<ITool> _tools = [];

    public IReadOnlyList<ITool> Tools => _tools;

    public void Register(ITool tool)
    {
        if (_tools.Any(t => t.Id == tool.Id))
            throw new InvalidOperationException($"工具 Id 重复：{tool.Id}");
        _tools.Add(tool);
    }
}
