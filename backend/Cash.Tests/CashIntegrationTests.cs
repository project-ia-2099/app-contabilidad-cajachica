using System.Net;
using System.Net.Http.Json;
using System.Text;
using Cash.Application.DTOs;
using Cash.Application.Services;
using Cash.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Cash.Tests;
public sealed class CashIntegrationTests : IDisposable
{
    readonly CashFactory factory = new();
    readonly HttpClient client;
    public CashIntegrationTests() => client = factory.CreateClient();
    public void Dispose() { client.Dispose(); factory.Dispose(); }
    async Task Open(decimal amount = 100) => (await client.PostAsJsonAsync("/api/fund", new { currency = "USD", amount })).EnsureSuccessStatusCode();
    async Task<VoucherView> Create(decimal amount = 80, string responsible = "Ana", string date = "2026-11-17")
    {
        var response = await client.PostAsJsonAsync("/api/vouchers", new { date, responsible, concept = "Movilidad", amount });
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<VoucherView>())!;
    }
    async Task Deliver(Guid id) => (await client.PostAsJsonAsync($"/api/vouchers/{id}/deliver", new { })).EnsureSuccessStatusCode();
    Task<HttpResponseMessage> Receipt(Guid id, decimal amount = 60, string reference = "F001-1", byte[]? bytes = null)
    {
        var form = new MultipartFormDataContent {
            { new StringContent("2026-11-17"), "date" }, { new StringContent(reference), "reference" },
            { new StringContent(amount.ToString(System.Globalization.CultureInfo.InvariantCulture)), "amount" },
            { new ByteArrayContent(bytes ?? Encoding.ASCII.GetBytes("%PDF-1.7\ncomprobante de prueba\n%%EOF")), "file", "comprobante.pdf" }
        };
        return client.PostAsync($"/api/vouchers/{id}/receipts", form);
    }
    [Fact] public async Task StartsWithoutInventedBalance()
    {
        var summary = (await client.GetFromJsonAsync<SummaryView>("/api/summary"))!;
        Assert.False(summary.Configured); Assert.Equal(0, summary.Balance);
    }
    [Fact] public async Task RequiresOpeningAndRejectsSecondOpening()
    {
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/vouchers", new { date = "2026-11-17", responsible = "Ana", concept = "Taxi", amount = 10 })).StatusCode);
        await Open(); Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/fund", new { currency = "USD", amount = 100 })).StatusCode);
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(1.001)]
    public async Task RejectsInvalidAmounts(decimal amount)
    {
        await Open(); Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/vouchers", new { date = "2026-11-17", responsible = "Ana", concept = "Taxi", amount })).StatusCode);
    }
    [Fact] public async Task NumbersAreSequentialAndRegistrationDoesNotChangeBalance()
    {
        await Open(); var first = await Create(); var second = await Create();
        Assert.Equal("VC-000001", first.Number); Assert.Equal("VC-000002", second.Number);
        Assert.Equal(100, (await client.GetFromJsonAsync<SummaryView>("/api/summary"))!.Balance);
    }
    [Fact] public async Task CannotOverdrawOrDeliverTwice()
    {
        await Open(); var first = await Create(); var second = await Create(); await Deliver(first.Id);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/vouchers/{second.Id}/deliver", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/vouchers/{first.Id}/deliver", new { })).StatusCode);
        Assert.Equal(20, (await client.GetFromJsonAsync<SummaryView>("/api/summary"))!.Balance);
    }
    [Fact] public async Task FullLifecycleBalancesAndPreservesFileAndAudit()
    {
        await Open(); var v = await Create(); await Deliver(v.Id); (await Receipt(v.Id)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/vouchers/{v.Id}/close", new { returned = 19 })).StatusCode);
        (await client.PostAsJsonAsync($"/api/vouchers/{v.Id}/close", new { returned = 20 })).EnsureSuccessStatusCode();
        var summary = (await client.GetFromJsonAsync<SummaryView>("/api/summary"))!;
        Assert.Equal(40, summary.Balance); Assert.Equal(60, summary.Spent); Assert.Equal(0, summary.Outstanding); Assert.Equal(1, summary.ClosedCount);
        var closed = (await client.GetFromJsonAsync<VoucherView>($"/api/vouchers/{v.Id}"))!;
        Assert.Equal("Closed", closed.Status); Assert.Equal(20, closed.Returned); Assert.NotNull(closed.ClosedAt);
        var download = await client.GetAsync($"/api/receipts/{closed.Receipts[0].Id}");
        Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType); Assert.StartsWith("%PDF-1.7", await download.Content.ReadAsStringAsync());
        var history = (await client.GetFromJsonAsync<List<MovementView>>("/api/movements"))!;
        Assert.Equal(5, history.Count); Assert.Equal(40, history.Sum(m => m.Amount));
    }
    [Fact] public async Task RejectsRepeatedClosureAndChangesAfterClosing()
    {
        await Open(); var v = await Create(); await Deliver(v.Id);
        (await client.PostAsJsonAsync($"/api/vouchers/{v.Id}/close", new { returned = 80 })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Receipt(v.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/vouchers/{v.Id}/close", new { returned = 80 })).StatusCode);
    }
    [Fact] public async Task ReceiptRequiresDeliveredVoucherAndRealFileType()
    {
        await Open(); var v = await Create(); Assert.Equal(HttpStatusCode.Conflict, (await Receipt(v.Id)).StatusCode); await Deliver(v.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await Receipt(v.Id, bytes: Encoding.UTF8.GetBytes("<script>bad</script>"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Receipt(v.Id, bytes: new byte[CashService.MaxFileSize + 1])).StatusCode);
    }
    [Fact] public async Task RejectsDuplicateAndExcessiveReceipts()
    {
        await Open(); var v = await Create(); await Deliver(v.Id); (await Receipt(v.Id, 30)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Receipt(v.Id, 10)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Receipt(v.Id, 60, "F001-2")).StatusCode);
    }
    [Fact] public async Task TopUpChangesBalanceAndIsAudited()
    {
        await Open(); (await client.PostAsJsonAsync("/api/fund/top-ups", new { amount = 25, reference = "Reposición 001" })).EnsureSuccessStatusCode();
        Assert.Equal(125, (await client.GetFromJsonAsync<SummaryView>("/api/summary"))!.Balance);
        Assert.Equal(2, (await client.GetFromJsonAsync<List<MovementView>>("/api/movements"))!.Count);
    }
    [Fact] public async Task ReportFiltersDatesAndCsvNeutralizesSpreadsheetFormulas()
    {
        await Open(); await Create(responsible: "=HYPERLINK(1)"); await Create(date: "2026-10-01");
        var report = (await client.GetFromJsonAsync<ReportView>("/api/reports?from=2026-11-01&to=2026-11-30"))!;
        Assert.Single(report.Vouchers); Assert.Equal(80, report.Requested);
        var csv = await client.GetStringAsync("/api/reports/export?from=2026-11-01&to=2026-11-30");
        Assert.Contains("'=HYPERLINK(1)", csv); Assert.DoesNotContain("2026-10-01", csv);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/reports?from=2026-12-01&to=2026-11-01")).StatusCode);
    }
    [Fact] public async Task ConcurrentFundChangesAreRejected()
    {
        await Open(); using var first = factory.Services.CreateScope(); using var second = factory.Services.CreateScope();
        var one = first.ServiceProvider.GetRequiredService<AppDbContext>(); var two = second.ServiceProvider.GetRequiredService<AppDbContext>();
        var f1 = (await one.Funds.FindAsync(1))!; var f2 = (await two.Funds.FindAsync(1))!;
        f1.Revision++; await one.SaveChangesAsync(); f2.Revision++;
        var service = second.ServiceProvider.GetRequiredService<CashService>();
        var error = await Assert.ThrowsAsync<BusinessException>(() => service.AddFunds(new(1, "Concurrente"), default)); Assert.Equal(409, error.Status);
    }
    [Fact] public async Task MissingVoucherReturns404() => Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/vouchers/{Guid.NewGuid()}")).StatusCode);
}