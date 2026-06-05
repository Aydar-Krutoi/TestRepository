using CommunityToolkit.Mvvm.ComponentModel;

namespace Ranil_Uchebka.Models;

public sealed class OrderEditorState
{
    public int Number { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.Today;
    public string OrderCode { get; set; } = string.Empty;
    public string OrderName { get; set; } = string.Empty;
    public string ProductName { get; set; } = "Конвейер";
    public string CustomerLogin { get; set; } = string.Empty;
    public string? ManagerLogin { get; set; }
    public decimal? Cost { get; set; }
    public DateTime? PlannedCompletionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string StatusName { get; set; } = "Новый";
    public long StatusId { get; set; }
    public bool IsNew { get; set; } = true;
    public List<OrderDimensionRow> Dimensions { get; } = [];
    public List<OrderAttachmentRow> Attachments { get; } = [];
}

public sealed class OrderDimensionRow : ObservableObject
{
    public static readonly string[] NameOptions = ["Длина", "Ширина", "Высота"];
    public static readonly string[] UnitOptions = ["мм", "м", "см", "м²", "шт"];

    public long DimensionId { get; set; }
    public string Name { get; set; } = NameOptions[0];
    public string Unit { get; set; } = UnitOptions[0];
    public decimal Value { get; set; }

    public string ValueText
    {
        get => Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Value = 0;
                return;
            }

            if (decimal.TryParse(value.Replace(',', '.'), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var v))
            {
                Value = v;
            }
        }
    }

    public string DisplayText => $"{Name}: {ValueText} {Unit}";
}

public sealed class OrderHistoryItem
{
    public DateTime ChangedAt { get; init; }
    public string StatusName { get; init; } = string.Empty;
    public string? ChangedBy { get; init; }
    public string? Comment { get; init; }
}

public sealed class UserOption
{
    public string Login { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}
