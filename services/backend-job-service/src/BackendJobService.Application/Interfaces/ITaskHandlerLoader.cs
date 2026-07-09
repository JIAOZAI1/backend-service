using BackendJobService.Contracts;

namespace BackendJobService.Application.Interfaces;

/// <summary>
/// 根据 JobTask 上配置的插件程序集与类型名，反射加载并实例化 ITaskHandler。
/// </summary>
public interface ITaskHandlerLoader
{
    ITaskHandler CreateHandler(string pluginAssembly, string handlerType);
}
