using System.Reflection;
using System.Runtime.Loader;

namespace BackendJobService.Infrastructure.Plugins;

/// <summary>
/// 每个插件程序集专属的加载上下文。用 AssemblyDependencyResolver 解析插件目录下的同级依赖
/// （如插件自带的第三方库），但 BackendJobService.Contracts 主动放行给默认加载上下文解析，
/// 保证 ITaskHandler / TaskResult 等契约类型与宿主进程是同一个运行时类型（否则跨 ALC 的接口
/// 类型不兼容，宿主侧的类型转换、反射调用会在运行时以隐蔽的方式失败)。
/// </summary>
public class PluginLoadContext : AssemblyLoadContext
{
    private const string SharedContractsAssemblyName = "BackendJobService.Contracts";

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginAssemblyPath)
        : base(name: $"plugin:{Path.GetFileName(pluginAssemblyPath)}", isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (string.Equals(assemblyName.Name, SharedContractsAssemblyName, StringComparison.Ordinal))
        {
            // 返回 null：交回默认解析流程，从而复用宿主已加载的同一份 Contracts 程序集
            return null;
        }

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }
}
