using BackendJobService.Plugins.MySql;
using Shouldly;

namespace BackendJobService.UnitTests.PluginHandlers;

public class MySqlPluginHelperTests
{
    [Theory]
    [InlineData("example_db")]
    [InlineData("Db01")]
    [InlineData("_x")]
    public void IsValidIdentifier_LegalNames_ReturnsTrue(string value) =>
        MySqlPluginHelper.IsValidIdentifier(value).ShouldBeTrue();

    [Theory]
    [InlineData("bad-name")]
    [InlineData("db`; DROP DATABASE x")]
    [InlineData("库")]
    [InlineData("")]
    [InlineData("a b")]
    public void IsValidIdentifier_IllegalNames_ReturnsFalse(string value) =>
        MySqlPluginHelper.IsValidIdentifier(value).ShouldBeFalse();

    [Theory]
    [InlineData("%")]
    [InlineData("192.168.8.184")]
    [InlineData("10.0.%.%")]
    [InlineData("app-node-1.internal")]
    public void IsValidHost_LegalHosts_ReturnsTrue(string value) =>
        MySqlPluginHelper.IsValidHost(value).ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("h'ost")]
    [InlineData("host;1")]
    public void IsValidHost_IllegalHosts_ReturnsFalse(string value) =>
        MySqlPluginHelper.IsValidHost(value).ShouldBeFalse();

    [Theory]
    [InlineData("ALL PRIVILEGES")]
    [InlineData("select")]
    [InlineData("Create View")]
    public void IsAllowedPrivilege_Whitelisted_ReturnsTrue(string value) =>
        MySqlPluginHelper.IsAllowedPrivilege(value).ShouldBeTrue();

    [Theory]
    [InlineData("SUPER")]
    [InlineData("GRANT OPTION")]
    [InlineData("SELECT; DROP TABLE x")]
    public void IsAllowedPrivilege_NotWhitelisted_ReturnsFalse(string value) =>
        MySqlPluginHelper.IsAllowedPrivilege(value).ShouldBeFalse();

    [Fact]
    public void EscapeStringLiteral_EscapesQuotesAndBackslashes()
    {
        MySqlPluginHelper.EscapeStringLiteral(@"pa'ss\word").ShouldBe(@"pa''ss\\word");
    }

    [Fact]
    public void QuoteIdentifier_WrapsInBackticks()
    {
        MySqlPluginHelper.QuoteIdentifier("example_db").ShouldBe("`example_db`");
    }
}
