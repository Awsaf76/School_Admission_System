namespace SchoolAdmission.Api.Models;

public class SchoolClass
{
    public string Id { get; set; } = $"c_{Guid.NewGuid():N}";
    public string Name { get; set; } = "";
    public int TotalSeats { get; set; }
    public List<AdmissionHold> Holds { get; set; } = [];
}

public enum HoldStatus { Held = 0, Confirmed = 1, Expired = 2, Cancelled = 3 }

public class AdmissionHold
{
    public string Id { get; set; } = $"h_{Guid.NewGuid():N}";
    public string ClassId { get; set; } = "";
    public SchoolClass Class { get; set; } = null!;
    public string StudentId { get; set; } = "";
    public HoldStatus Status { get; set; } = HoldStatus.Held;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
}

public record ClassView(string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats)
{
    public static ClassView From(SchoolClass item, IEnumerable<AdmissionHold> holds)
    {
        var list = holds.Where(h => h.ClassId == item.Id).ToList();
        var held = list.Count(h => h.Status == HoldStatus.Held);
        var confirmed = list.Count(h => h.Status == HoldStatus.Confirmed);
        return new(item.Id, item.Name, item.TotalSeats, held, confirmed,
            Math.Max(0, item.TotalSeats - held - confirmed));
    }
}
