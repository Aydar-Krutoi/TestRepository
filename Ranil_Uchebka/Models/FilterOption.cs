namespace Ranil_Uchebka.Models;

public sealed class FilterOption
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;

    public override string ToString() => DisplayName;
}
