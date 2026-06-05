namespace Ranil_Uchebka.Models;

public sealed class OrderAttachmentRow
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
