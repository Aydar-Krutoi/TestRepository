namespace Ranil_Uchebka.Models;

public sealed class WorkerItem
{
    public long WorkerId { get; init; }
    public string FullName { get; set; } = string.Empty;
    public DateTime BirthDate { get; set; }
    public string HomeAddress { get; set; } = string.Empty;
    public string Education { get; set; } = string.Empty;
    public string Qualification { get; set; } = string.Empty;
    public string OperationsCsv { get; set; } = string.Empty;

    public string Surname
    {
        get
        {
            var split = FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return split.Length > 0 ? split[0] : FullName;
        }
    }

    public int Age
    {
        get
        {
            var now = DateTime.Today;
            var age = now.Year - BirthDate.Year;
            if (BirthDate.Date > now.AddYears(-age))
            {
                age--;
            }
            return Math.Max(age, 0);
        }
    }
}
