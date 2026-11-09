using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public record CreateHoldResult(AdmissionHold? Hold, string? Error);

public static class HoldService
{
    public static Task<int> ExpireAllAsync(AdmissionDbContext db) =>
        db.Holds.Where(h => h.Status == HoldStatus.Held && h.ExpiresAt <= DateTimeOffset.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Status, HoldStatus.Expired));

    public static async Task<AdmissionHold?> GetAndRefreshAsync(AdmissionDbContext db, string id)
    {
        var hold = await db.Holds.FirstOrDefaultAsync(h => h.Id == id);
        if (hold is not null && hold.Status == HoldStatus.Held && hold.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            hold.Status = HoldStatus.Expired;
            await db.SaveChangesAsync();
        }
        return hold;
    }

    public static async Task<CreateHoldResult> CreateAsync(AdmissionDbContext db, string classId, string studentId, int duration)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        // Prevent the same student from applying to two classes at the same time.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({studentId}))");
        // Lock the class row: concurrent requests cannot take the same last seat.
        var schoolClass = await db.Classes.FromSqlInterpolated(
            $"SELECT * FROM \"Classes\" WHERE \"Id\" = {classId} FOR UPDATE").FirstOrDefaultAsync();
        if (schoolClass is null) { await tx.RollbackAsync(); return new(null, "CLASS_NOT_FOUND"); }

        await ExpireAllAsync(db);
        var alreadyActive = await db.Holds.AnyAsync(h => h.StudentId == studentId &&
            (h.Status == HoldStatus.Held || h.Status == HoldStatus.Confirmed));
        if (alreadyActive) { await tx.RollbackAsync(); return new(null, "ACTIVE_APPLICATION"); }

        var occupied = await db.Holds.CountAsync(h => h.ClassId == classId &&
            (h.Status == HoldStatus.Held || h.Status == HoldStatus.Confirmed));
        if (occupied >= schoolClass.TotalSeats) { await tx.RollbackAsync(); return new(null, "NO_SEATS"); }

        var now = DateTimeOffset.UtcNow;
        var hold = new AdmissionHold { ClassId = classId, StudentId = studentId,
            CreatedAt = now, ExpiresAt = now.AddSeconds(duration), Status = HoldStatus.Held };
        db.Holds.Add(hold);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new(hold, null);
    }

    public static async Task<string> ConfirmAsync(AdmissionDbContext db, string holdId, string studentId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var hold = await db.Holds.FromSqlInterpolated(
            $"SELECT * FROM \"Holds\" WHERE \"Id\" = {holdId} FOR UPDATE").FirstOrDefaultAsync();
        if (hold is null) return "NOT_FOUND";
        if (hold.StudentId != studentId) return "NOT_OWNER";
        if (hold.Status == HoldStatus.Confirmed) { await tx.CommitAsync(); return "CONFIRMED"; }
        if (hold.Status == HoldStatus.Cancelled) return "CANCELLED";
        if (hold.Status == HoldStatus.Expired || hold.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            hold.Status = HoldStatus.Expired;
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return "EXPIRED";
        }
        hold.Status = HoldStatus.Confirmed;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return "CONFIRMED";
    }

    public static async Task<string> CancelAsync(AdmissionDbContext db, string holdId, string studentId)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var hold = await db.Holds.FromSqlInterpolated(
            $"SELECT * FROM \"Holds\" WHERE \"Id\" = {holdId} FOR UPDATE").FirstOrDefaultAsync();
        if (hold is null) return "NOT_FOUND";
        if (hold.StudentId != studentId) return "NOT_OWNER";
        if (hold.Status == HoldStatus.Confirmed) return "CONFIRMED";
        if (hold.Status == HoldStatus.Held) { hold.Status = HoldStatus.Cancelled; await db.SaveChangesAsync(); }
        await tx.CommitAsync();
        return "CANCELLED";
    }
}
