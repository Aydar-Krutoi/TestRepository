namespace Ranil_Uchebka.Models;

public sealed class WarehouseOption
{
    public long WarehouseId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DisplayName => WarehouseId == 0 ? Name : $"{Name} (№{WarehouseId})";
}
