using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using BackendJobService.Application.Interfaces;
using BackendJobService.Contracts;
using Microsoft.Extensions.Logging;

namespace BackendJobService.Infrastructure.Plugins;

/// <summary>
/// 固定插件目录（plugins/），服务启动时扫描并按需加载程序集。每个插件程序集使用独立的
/// AssemblyLoadContext（不可卸载），避免不同插件之间依赖冲突；新增/替换插件需要重启服务生效。
/// </summary>
public class TaskHandlerLoader : ITaskHandlerLoader
{
    private readonly string _pluginDirectory;
    private readonly ILogger<TaskHandlerLoader> _logger;
    private readonly ConcurrentDictionary<string, AssemblyLoadContext> _loadContexts = new();

    public TaskHandlerLoader(string pluginDirectory, ILogger<TaskHandlerLoader> logger)
    {
        // AssemblyLoadContext.LoadFromAssemblyPath 要求绝对路径；配置里 Plugins:Directory
        // 通常是相对路径（如 "plugins"），这里统一相对于当前工作目录解析成绝对路径。
        _pluginDirectory = Path.GetFullPath(pluginDirectory);
        _logger = logger;
    }

    public ITaskHandler CreateHandler(string pluginAssembly, string handlerType)
    {
        var assembly = LoadPluginAssembly(pluginAssembly);

        var type = assembly.GetType(handlerType)
            ?? throw new InvalidOperationException($"type '{handlerType}' not found in assembly '{pluginAssembly}'");

        if (!typeof(ITaskHandler).IsAssignableFrom(type))
        {
            throw new InvalidOperationException($"type '{handlerType}' does not implement {nameof(ITaskHandler)}");
        }

        var instance = Activator.CreateInstance(type)
            ?? throw new InvalidOperationException($"failed to instantiate '{handlerType}' (missing public parameterless constructor?)");

        return (ITaskHandler)instance;
    }

    private Assembly LoadPluginAssembly(string pluginAssembly)
    {
        var path = Path.Combine(_pluginDirectory, pluginAssembly);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"plugin assembly not found: {path}", path);
        }

        var context = _loadContexts.GetOrAdd(pluginAssembly, name =>
        {
            _logger.LogInformation("Loading plugin assembly {PluginAssembly} from {Path}", name, path);
            return new PluginLoadContext(path);
        });

        // 同一个 AssemblyLoadContext 内重复 LoadFromAssemblyPath 会返回已加载的程序集，天然幂等
        return context.LoadFromAssemblyPath(path);
    }
}
