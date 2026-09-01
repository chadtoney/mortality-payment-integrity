using MortalityReview.Application;
using MortalityReview.Domain;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IMatcher, DeterministicMatcher>();
builder.Services.AddSingleton<DemoCaseStore>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/api/dashboard", (DemoCaseStore store) => Results.Ok(store.GetDashboard()));
app.MapGet("/api/cases", (DemoCaseStore store) => Results.Ok(store.GetCases()));
app.MapGet("/api/cases/{caseId}", (string caseId, DemoCaseStore store) =>
    store.GetCase(caseId) is { } investigationCase
        ? Results.Ok(investigationCase)
        : Results.NotFound());
app.MapGet("/api/cases/{caseId}/audit", (string caseId, DemoCaseStore store) =>
    store.GetCase(caseId) is null
        ? Results.NotFound()
        : Results.Ok(store.GetAuditEvents(caseId)));
app.MapPatch("/api/cases/{caseId}/decision", (
    string caseId,
    ReviewDecision request,
    DemoCaseStore store) =>
{
    try
    {
        return store.RecordDecision(caseId, request.Status, request.Note) is { } updated
            ? Results.Ok(updated)
            : Results.NotFound();
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});
app.MapPost("/api/demo/reset", (DemoCaseStore store) => Results.Ok(store.Reset()));
app.MapFallbackToFile("index.html");

app.Run();

public sealed record ReviewDecision(InvestigationStatus Status, string? Note);
