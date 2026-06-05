using Ranil_Uchebka.Services;

namespace Ranil_Uchebka.LogicTests;

public sealed class PasswordPolicyServiceTests
{
    private readonly PasswordPolicyService _service = new();

    [Fact]
    public void Validate_Fails_WhenPasswordTooShort()
    {
        var result = _service.Validate("A1x");

        Assert.False(result.isValid);
        Assert.Equal("Пароль должен содержать от 4 до 16 символов.", result.message);
    }

    [Fact]
    public void Validate_Fails_WhenPasswordContainsForbiddenSymbols()
    {
        var result = _service.Validate("Aa1*");

        Assert.False(result.isValid);
        Assert.Equal("Пароль не должен содержать символы: * & { } | +", result.message);
    }

    [Fact]
    public void Validate_Fails_WhenPasswordHasNoUppercase()
    {
        var result = _service.Validate("abcd1");

        Assert.False(result.isValid);
        Assert.Equal("Пароль должен содержать хотя бы одну заглавную букву.", result.message);
    }

    [Fact]
    public void Validate_Fails_WhenPasswordHasNoDigit()
    {
        var result = _service.Validate("Abcd");

        Assert.False(result.isValid);
        Assert.Equal("Пароль должен содержать хотя бы одну цифру.", result.message);
    }

    [Fact]
    public void Validate_Succeeds_ForStrongPassword()
    {
        var result = _service.Validate("StrongPass9");

        Assert.True(result.isValid);
        Assert.Equal("OK", result.message);
    }
}
