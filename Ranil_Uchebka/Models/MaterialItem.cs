namespace Ranil_Uchebka.Models;

public sealed class MaterialItem
{
    public string Article { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public string? MainSupplier { get; set; }
    public int? DeliveryDays { get; set; }
    public string MaterialType { get; set; } = string.Empty;
    public string? Gost { get; set; }
    public decimal? Length { get; set; }
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
}
