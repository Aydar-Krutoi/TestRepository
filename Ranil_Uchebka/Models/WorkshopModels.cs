using CommunityToolkit.Mvvm.ComponentModel;

namespace Ranil_Uchebka.Models;

public sealed class WorkshopInfo
{
    public long WorkshopId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string PlanFileName { get; init; } = string.Empty;
}

public sealed class WorkshopMarkerModel
{
    public long MarkerId { get; set; }
    public string IconType { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }

    public string IconDisplayName => WorkshopIconTypes.GetDisplayName(IconType);
}

public static class WorkshopIconTypes
{
  public const string Equipment = "Equipment";
  public const string FireExtinguisher = "FireExtinguisher";
  public const string FirstAid = "FirstAid";
  public const string Exit = "Exit";

  private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.Ordinal)
  {
      [Equipment] = "Оборудование",
      [FireExtinguisher] = "Огнетушитель",
      [FirstAid] = "Аптечка",
      [Exit] = "Выход"
  };

  public static string GetDisplayName(string iconType) =>
      DisplayNames.TryGetValue(iconType, out var name) ? name : iconType;

  public static IReadOnlyList<FilterOption> GetFilterOptions() =>
      DisplayNames.Select(p => new FilterOption { Key = p.Key, DisplayName = p.Value }).ToList();
}

public sealed class EquipmentFailureRow
{
    public long FailureId { get; init; }
    public string EquipmentMarking { get; init; } = string.Empty;
    public string ReasonName { get; init; } = string.Empty;
    public DateTime StartedAt { get; init; }
    public DateTime? EndedAt { get; init; }
    public string? RegisteredBy { get; init; }
}

public partial class QualityCheckRow : ObservableObject
{
    public long ParameterId { get; init; }
    public string ParameterName { get; init; } = string.Empty;

    [ObservableProperty]
    private bool? _isPositive;

    [ObservableProperty]
    private string _comment = string.Empty;

    public bool IsPlusSelected => IsPositive == true;

    public bool IsMinusSelected => IsPositive == false;

    partial void OnIsPositiveChanged(bool? value)
    {
        OnPropertyChanged(nameof(IsPlusSelected));
        OnPropertyChanged(nameof(IsMinusSelected));
    }
}
