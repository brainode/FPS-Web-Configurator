using control_panel.Services;

namespace control_panel.Tests.Services;

public sealed class Pbkdf2PasswordHasherTests
{
    [Fact]
    public void VerifyPassword_ReturnsTrue_ForMatchingPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var passwordHash = hasher.HashPassword("change-me");

        var result = hasher.VerifyPassword("change-me", passwordHash);

        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_ReturnsFalse_ForDifferentPassword()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var passwordHash = hasher.HashPassword("change-me");

        var result = hasher.VerifyPassword("not-the-same", passwordHash);

        Assert.False(result);
    }
}
