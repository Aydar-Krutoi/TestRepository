using System.Text.RegularExpressions;

namespace Ranil_Uchebka.Services;

public sealed class PasswordPolicyService
{
    private static readonly Regex UppercaseRegex = new(@"\p{Lu}", RegexOptions.Compiled);
    private static readonly Regex DigitRegex = new(@"\d", RegexOptions.Compiled);
    private static readonly char[] Forbidden = ['*', '&', '{', '}', '|', '+'];

    public (bool isValid, string message) Validate(string password)
    {
        if (password.Length is < 4 or > 16)
        {
            return (false, "Пароль должен содержать от 4 до 16 символов.");
        }

        if (password.IndexOfAny(Forbidden) >= 0)
        {
            return (false, "Пароль не должен содержать символы: * & { } | +");
        }

        if (!UppercaseRegex.IsMatch(password))
        {
            return (false, "Пароль должен содержать хотя бы одну заглавную букву.");
        }

        if (!DigitRegex.IsMatch(password))
        {
            return (false, "Пароль должен содержать хотя бы одну цифру.");
        }

        return (true, "OK");
    }
}
