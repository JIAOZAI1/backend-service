using Shouldly;
using TenantDbConnector;

namespace TenantDbConnector.Tests;

public class TenantDbConnectorImplTests
{
    private static TenantDbInfo TestInfo(string tenantCode) => new(
        TenantCode: tenantCode,
        DbHost: "127.0.0.1",
        DbPort: 3306,
        DbName: $"{tenantCode}_db",
        DbUsername: "app",
        DbPassword: "secret");

    [Fact]
    public void Constructor_NullSsoGetter_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new TenantDbConnectorImpl(null, null!));
    }

    [Fact]
    public async Task GetTenantDbInfoAsync_ReturnsSsoGetterResult()
    {
        await using var connector = new TenantDbConnectorImpl(null, (code, ct) => Task.FromResult(TestInfo(code)));

        var info = await connector.GetTenantDbInfoAsync("acme");

        info.ShouldBe(TestInfo("acme"));
    }

    [Fact]
    public async Task GetTenantDbInfoAsync_LocalCacheHit_DoesNotCallSsoGetterAgain()
    {
        var calls = 0;
        await using var connector = new TenantDbConnectorImpl(null, (code, ct) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(TestInfo(code));
        });

        await connector.GetTenantDbInfoAsync("acme");
        await connector.GetTenantDbInfoAsync("acme");

        calls.ShouldBe(1);
    }

    [Fact]
    public async Task GetTenantDbInfoAsync_LocalCacheExpired_CallsSsoGetterAgain()
    {
        var calls = 0;
        await using var connector = new TenantDbConnectorImpl(null, (code, ct) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(TestInfo(code));
        }, new TenantDbConnectorOptions { LocalCacheTtl = TimeSpan.FromMilliseconds(1) });

        await connector.GetTenantDbInfoAsync("acme");
        await Task.Delay(20);
        await connector.GetTenantDbInfoAsync("acme");

        calls.ShouldBe(2);
    }

    [Fact]
    public async Task GetTenantDbInfoAsync_SsoGetterThrows_Propagates()
    {
        await using var connector = new TenantDbConnectorImpl(null,
            (code, ct) => Task.FromException<TenantDbInfo>(new TenantNotFoundException(code)));

        await Should.ThrowAsync<TenantNotFoundException>(() => connector.GetTenantDbInfoAsync("acme"));
    }

    [Fact]
    public async Task GetTenantDbInfoAsync_ConcurrentMiss_SingleFlightsSsoGetter()
    {
        var calls = 0;
        var gate = new TaskCompletionSource();
        await using var connector = new TenantDbConnectorImpl(null, async (code, ct) =>
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return TestInfo(code);
        });

        var tasks = Enumerable.Range(0, 20)
            .Select(_ => connector.GetTenantDbInfoAsync("acme"))
            .ToArray();

        await Task.Delay(20); // let all callers reach the blocking getter
        gate.SetResult();

        var results = await Task.WhenAll(tasks);

        results.ShouldAllBe(r => r == TestInfo("acme"));
        calls.ShouldBe(1);
    }

    [Fact]
    public async Task GetTenantDbInfoAsync_DifferentTenants_AreIndependent()
    {
        await using var connector = new TenantDbConnectorImpl(null, (code, ct) => Task.FromResult(TestInfo(code)));

        var acme = await connector.GetTenantDbInfoAsync("acme");
        var globex = await connector.GetTenantDbInfoAsync("globex");

        acme.TenantCode.ShouldBe("acme");
        globex.TenantCode.ShouldBe("globex");
    }

    [Fact]
    public async Task GetDataSourceAsync_ReusesDataSourceAcrossCalls()
    {
        await using var connector = new TenantDbConnectorImpl(null, (code, ct) => Task.FromResult(TestInfo(code)));

        var ds1 = await connector.GetDataSourceAsync("acme");
        var ds2 = await connector.GetDataSourceAsync("acme");

        ds1.ShouldBeSameAs(ds2);
    }

    [Fact]
    public async Task GetDataSourceAsync_DifferentTenants_GetDifferentDataSources()
    {
        await using var connector = new TenantDbConnectorImpl(null, (code, ct) => Task.FromResult(TestInfo(code)));

        var acme = await connector.GetDataSourceAsync("acme");
        var globex = await connector.GetDataSourceAsync("globex");

        acme.ShouldNotBeSameAs(globex);
    }

    [Fact]
    public async Task InvalidateTenantDbInfoAsync_ForcesDataSourceRebuild()
    {
        await using var connector = new TenantDbConnectorImpl(null, (code, ct) => Task.FromResult(TestInfo(code)));

        var ds1 = await connector.GetDataSourceAsync("acme");
        await connector.InvalidateTenantDbInfoAsync("acme");
        var ds2 = await connector.GetDataSourceAsync("acme");

        ds1.ShouldNotBeSameAs(ds2);
    }

    [Fact]
    public async Task InvalidateTenantDbInfoAsync_ForcesSsoGetterRefetch()
    {
        var calls = 0;
        await using var connector = new TenantDbConnectorImpl(null, (code, ct) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(TestInfo(code));
        });

        await connector.GetTenantDbInfoAsync("acme");
        await connector.InvalidateTenantDbInfoAsync("acme");
        await connector.GetTenantDbInfoAsync("acme");

        calls.ShouldBe(2);
    }

    [Fact]
    public async Task DisposeAsync_RejectsFurtherCalls()
    {
        var connector = new TenantDbConnectorImpl(null, (code, ct) => Task.FromResult(TestInfo(code)));

        await connector.DisposeAsync();
        await connector.DisposeAsync(); // idempotent

        await Should.ThrowAsync<ObjectDisposedException>(() => connector.GetTenantDbInfoAsync("acme"));
        await Should.ThrowAsync<ObjectDisposedException>(() => connector.GetDataSourceAsync("acme"));
        await Should.ThrowAsync<ObjectDisposedException>(() => connector.InvalidateTenantDbInfoAsync("acme"));
    }

    [Fact]
    public async Task GetDataSourceAsync_CustomConnectionStringBuilder_IsUsed()
    {
        TenantDbInfo? gotInfo = null;
        await using var connector = new TenantDbConnectorImpl(null,
            (code, ct) => Task.FromResult(TestInfo(code)),
            new TenantDbConnectorOptions
            {
                ConnectionStringBuilder = (info, opts) =>
                {
                    gotInfo = info;
                    return DefaultConnectionStringBuilder.Build(info, opts);
                },
            });

        await connector.GetDataSourceAsync("acme");

        gotInfo.ShouldBe(TestInfo("acme"));
    }
}
