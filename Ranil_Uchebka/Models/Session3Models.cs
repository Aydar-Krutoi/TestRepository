using CommunityToolkit.Mvvm.ComponentModel;

namespace Ranil_Uchebka.Models;

public sealed class ProductOption
{
    public string Name { get; init; } = string.Empty;
    public string Dimensions { get; init; } = string.Empty;

    public override string ToString() => Name;
}

public partial class SpecMaterialRow : ObservableObject
{
    public string Article { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    [ObservableProperty]
    private decimal _quantity;
}

public partial class SpecComponentRow : ObservableObject
{
    public string Article { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    [ObservableProperty]
    private decimal _quantity;
}

public partial class SpecAssemblyRow : ObservableObject
{
    public string ProductName { get; set; } = string.Empty;

    [ObservableProperty]
    private decimal _quantity;
}

public partial class SpecOperationRow : ObservableObject
{
    [ObservableProperty]
    private int _sequenceNumber;

    [ObservableProperty]
    private string _operationName = string.Empty;

    [ObservableProperty]
    private string _equipmentTypeName = string.Empty;

    [ObservableProperty]
    private int _operationTimeMin;

    [ObservableProperty]
    private string _description = string.Empty;
}

public sealed class RequirementRow
{
    public string Kind { get; init; } = string.Empty;
    public string Article { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public decimal RequiredQuantity { get; init; }
    public decimal AvailableQuantity { get; init; }
    public decimal MissingQuantity => Math.Max(RequiredQuantity - AvailableQuantity, 0);
    public decimal PurchasePrice { get; init; }
    public decimal Cost => RequiredQuantity * PurchasePrice;
    public int DeliveryDays { get; init; }
}

public sealed class GanttRow
{
    public string Equipment { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public string OperationName { get; init; } = string.Empty;
    public int StartMinute { get; init; }
    public int DurationMinute { get; init; }
    public int EndMinute => StartMinute + DurationMinute;
    public string Display => $"{ProductName}: {OperationName} ({StartMinute}-{EndMinute} мин.)";
}

public sealed class StockReportRow
{
    public string WarehouseName { get; init; } = string.Empty;
    public string TypeName { get; init; } = string.Empty;
    public string Article { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = string.Empty;
}

public sealed class StockReportWarehouseGroup
{
    public string WarehouseName { get; init; } = string.Empty;
    public decimal TotalQuantity { get; init; }
    public IReadOnlyList<StockReportRow> Rows { get; init; } = [];
}

public partial class ProductDimensionRow : ObservableObject
{
    public long DimensionId { get; set; }

    [ObservableProperty]
    private string _dimensionName = string.Empty;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private decimal _dimensionValue;
}

public sealed class ProductAttachmentRow
{
    public long AttachmentId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public byte[]? PendingData { get; init; }

    public bool IsPending => AttachmentId == 0;
    public bool CanExport => !IsPending;

    public string DisplayText => IsPending && PendingData is not null
        ? $"{FileName} ({PendingData.Length / 1024} КБ, не сохранён)"
        : FileName;
}
