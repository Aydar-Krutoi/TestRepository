namespace Ranil_Uchebka.Models;

public sealed class OrderListItem
{
    public int Number { get; init; }
    public DateTime OrderDate { get; init; }
    public string OrderCode { get; init; } = string.Empty;
    public string OrderName { get; init; } = string.Empty;
    public string StatusName { get; init; } = string.Empty;
    public long StatusId { get; init; }
    public decimal? Cost { get; init; }
    public string CustomerLogin { get; init; } = string.Empty;
    public string CustomerName { get; init; } = string.Empty;
    public DateTime? PlannedCompletionDate { get; init; }
    public string? ManagerLogin { get; init; }
    public string? ManagerName { get; init; }
    public string ProductName { get; init; } = string.Empty;

    public string CostDisplay => Cost.HasValue ? $"{Cost.Value:0.##}" : "—";

    public string PlannedDateDisplay =>
        PlannedCompletionDate.HasValue ? PlannedCompletionDate.Value.ToString("dd.MM.yyyy") : "—";

    public string ManagerDisplay => string.IsNullOrWhiteSpace(ManagerName) ? "—" : ManagerName;
}
