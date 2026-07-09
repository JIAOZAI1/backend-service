using BackendJobService.Infrastructure.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace BackendJobService.UnitTests.Plugins;

public class TaskHandlerLoaderTests
{
    // 测试程序集自身的 DLL 就是一个"插件"：其所在目录当插件目录用，
    // 验证真实的反射加载/实例化路径，而不是 mock 掉它。
    private static readonly string TestAssemblyDirectory = AppContext.BaseDirectory;
    private static readonly string TestAssemblyFileName = typeof(FakeTaskHandler).Assembly.Location is { Length: > 0 } loc
        ? Path.GetFileName(loc)
        : "BackendJobService.UnitTests.dll";

    private readonly TaskHandlerLoader _sut = new(TestAssemblyDirectory, NullLogger<TaskHandlerLoader>.Instance);

    [Fact]
    public void CreateHandler_ValidHandler_ReturnsInstance()
    {
        var handler = _sut.CreateHandler(TestAssemblyFileName, typeof(FakeTaskHandler).FullName!);

        // 插件通过独立的 AssemblyLoadContext 加载，返回的实例与测试程序集默认加载上下文里的
        // FakeTaskHandler 是不同的运行时类型（type identity 按 ALC 隔离），因此不能用
        // ShouldBeOfType<FakeTaskHandler>() 断言具体类型，只能按接口契约和类型全名校验。
        handler.ShouldNotBeNull();
        handler.GetType().FullName.ShouldBe(typeof(FakeTaskHandler).FullName);
        handler.ShouldBeAssignableTo<Contracts.ITaskHandler>();
    }

    [Fact]
    public async Task CreateHandler_ExecutesSuccessfully()
    {
        var handler = _sut.CreateHandler(TestAssemblyFileName, typeof(FakeTaskHandler).FullName!);

        var result = await handler.ExecuteAsync(
            new Contracts.TaskExecutionContext { ParametersJson = "{}" },
            CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.OutputJson.ShouldBe("fake-output");
    }

    [Fact]
    public void CreateHandler_TypeDoesNotImplementITaskHandler_Throws()
    {
        Should.Throw<InvalidOperationException>(() =>
            _sut.CreateHandler(TestAssemblyFileName, typeof(NotATaskHandler).FullName!));
    }

    [Fact]
    public void CreateHandler_TypeNotFound_Throws()
    {
        Should.Throw<InvalidOperationException>(() =>
            _sut.CreateHandler(TestAssemblyFileName, "BackendJobService.UnitTests.Plugins.DoesNotExist"));
    }

    [Fact]
    public void CreateHandler_AssemblyNotFound_Throws()
    {
        Should.Throw<FileNotFoundException>(() =>
            _sut.CreateHandler("NoSuchAssembly.dll", typeof(FakeTaskHandler).FullName!));
    }

    [Fact]
    public void Constructor_RelativePluginDirectory_StillLoadsSuccessfully()
    {
        // 回归测试：配置里 Plugins:Directory 通常是相对路径（如 "plugins"），
        // AssemblyLoadContext.LoadFromAssemblyPath 要求绝对路径，曾经因为没有转换
        // 导致 "is not an absolute path" 报错（见 2026-07-09 联调发现的问题）。
        var relativeDir = Path.GetRelativePath(Directory.GetCurrentDirectory(), TestAssemblyDirectory);
        var loader = new TaskHandlerLoader(relativeDir, NullLogger<TaskHandlerLoader>.Instance);

        var handler = loader.CreateHandler(TestAssemblyFileName, typeof(FakeTaskHandler).FullName!);

        handler.ShouldNotBeNull();
    }
}
