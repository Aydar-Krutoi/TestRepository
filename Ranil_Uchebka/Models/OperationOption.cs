using CommunityToolkit.Mvvm.ComponentModel;

namespace Ranil_Uchebka.Models;

public partial class OperationOption : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public long OperationId { get; init; }
    public string Name { get; init; } = string.Empty;
}
