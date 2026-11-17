using System.Globalization;
using Cash.Application.DTOs;
using Cash.Application.Services;
using Cash.Infrastructure;
using Cash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connection) && !builder.Environment.IsEnvironment("Testing"))
    throw new InvalidOperationException("Configure ConnectionStrings__DefaultConnection antes de iniciar la API.");
builder.Services.AddInfrastructure(connection ?? "");
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CashService>();
builder.Services.AddProblemDetails();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 6 * 1024 * 1024);
var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => {
    if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
}));
var app = builder.Build();
app.UseExceptionHandler();
app.Use(async (context, next) => {
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("Cache-Control", "no-store");
    try { await next(context); }
    catch (BusinessException ex) { await Results.Problem(statusCode: ex.Status, title: ex.Message).ExecuteAsync(context); }
});
app.UseCors();
using (var scope = app.Services.CreateScope()) {
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational()) await db.Database.MigrateAsync();
}
app.MapGet("/health", async (AppDbContext db, CancellationToken ct) => await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503));
var api = app.MapGroup("/api");
api.MapGet("/summary", (CashService service, CancellationToken ct) => service.Summary(ct));
api.MapPost("/fund", async (OpenFundRequest request, CashService service, CancellationToken ct) => { await service.Open(request, ct); return Results.NoContent(); });
api.MapPost("/fund/top-ups", async (AddFundsRequest request, CashService service, CancellationToken ct) => { await service.AddFunds(request, ct); return Results.NoContent(); });
api.MapGet("/vouchers", (CashService service, CancellationToken ct) => service.List(ct));
api.MapGet("/vouchers/{id:guid}", (Guid id, CashService service, CancellationToken ct) => service.Get(id, ct));
api.MapPost("/vouchers", async (CreateVoucherRequest request, CashService service, CancellationToken ct) => { var voucher = await service.Create(request, ct); return Results.Created($"/api/vouchers/{voucher.Id}", voucher); });
api.MapPost("/vouchers/{id:guid}/deliver", async (Guid id, CashService service, CancellationToken ct) => { await service.Deliver(id, ct); return Results.NoContent(); });
api.MapPost("/vouchers/{id:guid}/close", async (Guid id, CloseVoucherRequest request, CashService service, CancellationToken ct) => { await service.Close(id, request, ct); return Results.NoContent(); });
api.MapPost("/vouchers/{id:guid}/receipts", async (Guid id, HttpRequest request, CashService service, CancellationToken ct) => {
    if (!request.HasFormContentType) return Results.Problem(statusCode: 400, title: "Envíe el comprobante como formulario con archivo.");
    var form = await request.ReadFormAsync(ct);
    var file = form.Files.GetFile("file");
    if (file is null || file.Length == 0 || file.Length > CashService.MaxFileSize) return Results.Problem(statusCode: 400, title: "Adjunte un archivo de hasta 5 MB.");
    if (!DateOnly.TryParseExact(form["date"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
        !decimal.TryParse(form["amount"], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
        return Results.Problem(statusCode: 400, title: "La fecha o el importe del comprobante no son válidos.");
    using var buffer = new MemoryStream(); await file.CopyToAsync(buffer, ct);
    await service.AddReceipt(id, new ReceiptInput(date, form["reference"].ToString(), amount, file.FileName, file.ContentType, buffer.ToArray()), ct);
    return Results.NoContent();
});
api.MapGet("/receipts/{id:guid}", async (Guid id, CashService service, CancellationToken ct) => { var r = await service.Download(id, ct); return Results.File(r.Content, r.ContentType, r.FileName); });
api.MapGet("/movements", (CashService service, CancellationToken ct) => service.Movements(ct));
api.MapGet("/reports", (DateOnly from, DateOnly to, CashService service, CancellationToken ct) => service.Report(from, to, ct));
api.MapGet("/reports/export", async (DateOnly from, DateOnly to, CashService service, CancellationToken ct) => Results.File(await service.Export(from, to, ct), "text/csv; charset=utf-8", $"vales-{from:yyyyMMdd}-{to:yyyyMMdd}.csv"));
app.Run();
public partial class Program;