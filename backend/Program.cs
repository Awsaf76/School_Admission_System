using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Models;
using SchoolAdmission.Api.Services;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=school_admission;Username=school_user;Password=school_password";
builder.Services.AddDbContext<AdmissionDbContext>(o => o.UseNpgsql(connection));
builder.Services.AddHostedService<HoldExpiryWorker>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddPolicy("frontend", p => p
    .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
    .AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.UseCors("frontend");
app.UseSwagger();
app.UseSwaggerUI();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AdmissionDbContext>();
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync(
        "CREATE UNIQUE INDEX IF NOT EXISTS \"IX_Holds_OneActiveHoldPerStudent\" ON \"Holds\" (\"StudentId\") WHERE \"Status\" IN (0, 1)");
}

app.MapGet("/", () => Results.Ok(new { service = "School Admission API", status = "running" }));

app.MapPost("/classes", async (CreateClassRequest request, AdmissionDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 120)
        return Results.BadRequest(new { message = "Class name is required (maximum 120 characters)." });
    if (request.TotalSeats < 1 || request.TotalSeats > 100000)
        return Results.BadRequest(new { message = "Total seats must be between 1 and 100000." });

    var item = new SchoolClass { Name = request.Name.Trim(), TotalSeats = request.TotalSeats };
    db.Classes.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/classes/{item.Id}", new { id = item.Id, name = item.Name, totalSeats = item.TotalSeats });
});

app.MapGet("/classes", async (AdmissionDbContext db) =>
{
    await HoldService.ExpireAllAsync(db);
    var classes = await db.Classes.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
    var holds = await db.Holds.AsNoTracking().ToListAsync();
    return Results.Ok(classes.Select(c => ClassView.From(c, holds)));
});

app.MapGet("/classes/{classId}", async (string classId, AdmissionDbContext db) =>
{
    await HoldService.ExpireAllAsync(db);
    var item = await db.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == classId);
    if (item is null) return Results.NotFound(new { message = "Class not found." });
    var holds = await db.Holds.AsNoTracking().Where(h => h.ClassId == classId).ToListAsync();
    return Results.Ok(ClassView.From(item, holds));
});

app.MapPost("/classes/{classId}/holds", async (string classId, HttpRequest request, AdmissionDbContext db, IConfiguration config) =>
{
    var studentId = request.Headers["X-Student-Id"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(studentId) || studentId.Length > 40)
        return Results.BadRequest(new { message = "A valid X-Student-Id header is required." });
    var seconds = int.TryParse(config["HOLD_DURATION_SECONDS"], out var value) && value > 0 ? value : 600;
    var result = await HoldService.CreateAsync(db, classId, studentId, seconds);
    if (result.Error == "CLASS_NOT_FOUND") return Results.NotFound(new { message = "Class not found." });
    if (result.Error == "NO_SEATS") return Results.Conflict(new { message = "No seats are currently available." });
    if (result.Error == "ACTIVE_APPLICATION") return Results.Conflict(new { message = "Student already has an active hold or confirmed admission." });
    var hold = result.Hold!;
    return Results.Created($"/holds/{hold.Id}", new {
        holdId = hold.Id, classId = hold.ClassId, studentId = hold.StudentId,
        status = hold.Status.ToString().ToUpperInvariant(), expiresAt = hold.ExpiresAt
    });
});

app.MapGet("/holds/{holdId}", async (string holdId, AdmissionDbContext db) =>
{
    var hold = await HoldService.GetAndRefreshAsync(db, holdId);
    if (hold is null) return Results.NotFound(new { message = "Hold not found." });
    return Results.Ok(new { holdId = hold.Id, classId = hold.ClassId,
        status = hold.Status.ToString().ToUpperInvariant(), expiresAt = hold.ExpiresAt });
});

app.MapPost("/holds/{holdId}/confirm", async (string holdId, HttpRequest request, AdmissionDbContext db) =>
{
    var studentId = request.Headers["X-Student-Id"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(studentId)) return Results.BadRequest(new { message = "X-Student-Id header is required." });
    var result = await HoldService.ConfirmAsync(db, holdId, studentId);
    return result switch {
        "NOT_FOUND" => Results.NotFound(new { message = "Hold not found." }),
        "NOT_OWNER" => Results.Json(new { message = "This hold belongs to another student." }, statusCode: 403),
        "EXPIRED" => Results.Conflict(new { message = "This hold has expired." }),
        "CANCELLED" => Results.Conflict(new { message = "This hold was cancelled." }),
        _ => Results.Ok(new { holdId, status = "CONFIRMED" })
    };
});

app.MapDelete("/holds/{holdId}", async (string holdId, HttpRequest request, AdmissionDbContext db) =>
{
    var studentId = request.Headers["X-Student-Id"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(studentId)) return Results.BadRequest(new { message = "X-Student-Id header is required." });
    var result = await HoldService.CancelAsync(db, holdId, studentId);
    return result switch {
        "NOT_FOUND" => Results.NotFound(new { message = "Hold not found." }),
        "NOT_OWNER" => Results.Json(new { message = "This hold belongs to another student." }, statusCode: 403),
        "CONFIRMED" => Results.Conflict(new { message = "Confirmed admission cannot be cancelled here." }),
        _ => Results.NoContent()
    };
});

app.MapGet("/me/holds", async (HttpRequest request, AdmissionDbContext db) =>
{
    var studentId = request.Headers["X-Student-Id"].ToString().Trim();
    if (string.IsNullOrWhiteSpace(studentId)) return Results.BadRequest(new { message = "X-Student-Id header is required." });
    await HoldService.ExpireAllAsync(db);
    var rows = await db.Holds.AsNoTracking().Where(h => h.StudentId == studentId)
        .OrderByDescending(h => h.CreatedAt)
        .Select(h => new { holdId = h.Id, classId = h.ClassId, className = h.Class.Name,
            status = h.Status.ToString().ToUpper(), expiresAt = h.ExpiresAt }).ToListAsync();
    return Results.Ok(rows);
});

app.Run();
public partial class Program { }
record CreateClassRequest(string Name, int TotalSeats);
