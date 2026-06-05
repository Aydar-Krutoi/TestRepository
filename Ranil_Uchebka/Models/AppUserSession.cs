namespace Ranil_Uchebka.Models;

public sealed class AppUserSession
{
    public string Login { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public string? FullName { get; init; }
}
