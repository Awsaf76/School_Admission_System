using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace SchoolAdmission.Tests;

public class AdmissionApiTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine").WithDatabase("admission_test")
        .WithUsername("test").WithPassword("test").Build();
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient client = null!;

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string,string?> {
                    ["ConnectionStrings:DefaultConnection"] = postgres.GetConnectionString(),
                    ["HOLD_DURATION_SECONDS"] = "60"
                }));
        });
        client = factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
        await postgres.DisposeAsync();
    }

    [Fact]
    public async Task CanCreateClassAndReadItsSeatCount()
    {
        var id = await CreateClass("Test class", 3);
        var item = await client.GetFromJsonAsync<ClassResult>($"/classes/{id}");
        Assert.NotNull(item);
        Assert.Equal(3, item!.AvailableSeats);
    }

    [Fact]
    public async Task StudentCannotHaveTwoActiveApplications()
    {
        var one = await CreateClass("Morning", 2);
        var two = await CreateClass("Afternoon", 2);
        Assert.Equal(HttpStatusCode.Created, (await Hold(one, "student-a")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Hold(two, "student-a")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentRequestsNeverOversellSeats()
    {
        var id = await CreateClass("Concurrency", 4);
        var responses = await Task.WhenAll(Enumerable.Range(1, 25)
            .Select(i => Hold(id, $"student-{i}")));
        Assert.Equal(4, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        var final = await client.GetFromJsonAsync<ClassResult>($"/classes/{id}");
        Assert.NotNull(final);
        Assert.Equal(4, final!.HeldSeats + final.ConfirmedSeats);
        Assert.Equal(0, final.AvailableSeats);
    }

    private async Task<string> CreateClass(string name, int seats)
    {
        var response = await client.PostAsJsonAsync("/classes", new { name, totalSeats = seats });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreatedClass>())!.Id;
    }

    private async Task<HttpResponseMessage> Hold(string id, string student)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/classes/{id}/holds")
        { Content = JsonContent.Create(new { }) };
        request.Headers.Add("X-Student-Id", student);
        return await client.SendAsync(request);
    }

    private record CreatedClass(string Id, string Name, int TotalSeats);
    private record ClassResult(string Id, string Name, int TotalSeats, int HeldSeats, int ConfirmedSeats, int AvailableSeats);
}
