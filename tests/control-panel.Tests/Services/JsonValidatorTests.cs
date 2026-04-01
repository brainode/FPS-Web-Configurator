using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class JsonValidatorTests
{
    [Fact]
    public void IsValid_ReturnsTrue_ForValidJson()
    {
        var result = JsonValidator.IsValid("{\"g_gametype\":\"ca\"}", out var error);

        Assert.True(result);
        Assert.Null(error);
    }

    [Fact]
    public void IsValid_ReturnsFalse_ForInvalidJson()
    {
        var result = JsonValidator.IsValid("{\"g_gametype\":", out var error);

        Assert.False(result);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
