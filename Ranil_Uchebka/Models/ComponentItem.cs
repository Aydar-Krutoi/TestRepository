namespace Ranil_Uchebka.Models;

public sealed class ComponentItem
{
    public string Article { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
    public decimal Weight { get; set; }
    public string? MainSupplier { get; set; }
    public int? DeliveryDays { get; set; }
    public string ComponentType { get; set; } = string.Empty;
    public long WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
}
