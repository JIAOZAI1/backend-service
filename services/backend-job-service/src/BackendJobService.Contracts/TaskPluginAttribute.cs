namespace BackendJobService.Contracts;

/// <summary>
/// 标注在 ITaskHandler 实现类上的插件元数据。定义在 Contracts 而非具体插件项目里，
/// 使宿主无需引用插件项目即可反射读取元数据（Contracts 由 PluginLoadContext 放行给
/// 默认加载上下文，跨 ALC 读取特性时类型一致）。
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TaskPluginAttribute : Attribute
{
    public TaskPluginAttribute(string name)
    {
        Name = name;
    }

    /// <summary>插件唯一名称，kebab-case，如 "mysql-create-database"。</summary>
    public string Name { get; }

    /// <summary>插件用途的一句话描述。</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>插件版本号，语义化版本格式。</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>插件作者/维护团队。</summary>
    public string Author { get; set; } = string.Empty;
}
