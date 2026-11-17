using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Cash.Application.DTOs;
using Cash.Domain.Entities;
using Cash.Domain.Interfaces;
namespace Cash.Application.Services;

public sealed class BusinessException(string message, int status = 400) : Exception(message)
{
    public int Status { get; } = status;
}
public sealed class CashService(ICashStore store, TimeProvider time)
{
    public const decimal MaxAmount = 999999999999.99m;
    public const int MaxFileSize = 5 * 1024 * 1024;
    static string Text(string? value, string field, int max)
    {
        value = value?.Trim();
        if (string.IsNullOrWhiteSpace(value) || value.Length > max)
            throw new BusinessException($"{field}: ingrese entre 1 y {max} caracteres.");
        return value;
    }
    static void Money(decimal amount, bool allowZero = false)
    {
        if (amount < 0 || (!allowZero && amount == 0) || amount > MaxAmount || decimal.Round(amount, 2) != amount)
            throw new BusinessException("Ingrese un importe válido con un máximo de dos decimales.");
    }
    static void ValidDate(DateOnly date)
    {
        if (date == default) throw new BusinessException("La fecha es obligatoria.");
    }
    async Task<Fund> Fund(CancellationToken ct) => await store.GetFund(ct) ?? throw new BusinessException("Primero abra la caja chica.", 409);
    async Task<Voucher> Voucher(Guid id, CancellationToken ct) => await store.GetVoucher(id, ct) ?? throw new BusinessException("El vale no existe.", 404);
    void Log(Fund fund, string type, string detail, decimal amount = 0, Guid? voucherId = null)
    {
        Money(fund.Balance + amount, true);
        fund.Balance += amount;
        fund.Revision++;
        store.AddMovement(new Movement { At = time.GetUtcNow(), Type = type, Detail = detail, Amount = amount, Balance = fund.Balance, VoucherId = voucherId });
    }
    static VoucherView View(Voucher v)
    {
        var spent = v.Receipts.Sum(r => r.Amount);
        return new(v.Id, $"VC-{v.Number:D6}", v.Date, v.Responsible, v.Concept, v.Amount, v.Status.ToString(), spent, v.Returned,
            v.Status == VoucherStatus.Delivered ? v.Amount - spent : 0, v.DeliveredAt, v.ClosedAt,
            v.Receipts.OrderBy(r => r.Date).Select(r => new ReceiptView(r.Id, r.Date, r.Reference, r.Amount, r.FileName)).ToList());
    }
    public async Task<SummaryView> Summary(CancellationToken ct)
    {
        var fund = await store.GetFund(ct);
        if (fund is null) return new(false, null, 0, 0, 0, 0, 0, 0);
        var vouchers = await store.GetVouchers(ct);
        return new(true, fund.Currency, fund.Balance, vouchers.Where(v => v.Status == VoucherStatus.Delivered).Sum(v => v.Amount),
            vouchers.Sum(v => v.Receipts.Sum(r => r.Amount)), vouchers.Count(v => v.Status == VoucherStatus.Pending),
            vouchers.Count(v => v.Status == VoucherStatus.Delivered), vouchers.Count(v => v.Status == VoucherStatus.Closed));
    }
    public async Task Open(OpenFundRequest request, CancellationToken ct)
    {
        if (await store.GetFund(ct) is not null) throw new BusinessException("La caja chica ya está abierta.", 409);
        var currency = Text(request.Currency, "Moneda", 3).ToUpperInvariant();
        if (!Regex.IsMatch(currency, "^[A-Z]{3}$")) throw new BusinessException("Use el código de moneda de tres letras, por ejemplo PEN, USD o EUR.");
        Money(request.Amount);
        var fund = new Fund { Currency = currency };
        store.AddFund(fund);
        Log(fund, "Apertura", "Fondo inicial de caja chica", request.Amount);
        await store.Save(ct);
    }
    public async Task AddFunds(AddFundsRequest request, CancellationToken ct)
    {
        Money(request.Amount);
        var reference = Text(request.Reference, "Referencia de reposición", 200);
        var fund = await Fund(ct);
        Log(fund, "Reposición", reference, request.Amount);
        await store.Save(ct);
    }
    public async Task<List<VoucherView>> List(CancellationToken ct) => (await store.GetVouchers(ct)).Select(View).ToList();
    public async Task<VoucherView> Get(Guid id, CancellationToken ct) => View(await Voucher(id, ct));
    public async Task<VoucherView> Create(CreateVoucherRequest request, CancellationToken ct)
    {
        Money(request.Amount); ValidDate(request.Date);
        var responsible = Text(request.Responsible, "Responsable", 160);
        var concept = Text(request.Concept, "Concepto", 500);
        var fund = await Fund(ct);
        var voucher = new Voucher { Number = fund.NextNumber++, Date = request.Date, Responsible = responsible, Concept = concept, Amount = request.Amount, CreatedAt = time.GetUtcNow() };
        store.AddVoucher(voucher);
        Log(fund, "Registro", $"VC-{voucher.Number:D6}: {responsible}", voucherId: voucher.Id);
        await store.Save(ct);
        return View(voucher);
    }
    public async Task Deliver(Guid id, CancellationToken ct)
    {
        var fund = await Fund(ct); var v = await Voucher(id, ct);
        if (v.Status != VoucherStatus.Pending) throw new BusinessException("Solo puede entregar un vale pendiente.", 409);
        if (v.Amount > fund.Balance) throw new BusinessException("El saldo de caja es insuficiente para esta entrega.", 409);
        v.Status = VoucherStatus.Delivered; v.DeliveredAt = time.GetUtcNow();
        Log(fund, "Entrega", $"Entrega de VC-{v.Number:D6} a {v.Responsible}", -v.Amount, v.Id);
        await store.Save(ct);
    }
    public async Task AddReceipt(Guid id, ReceiptInput request, CancellationToken ct)
    {
        Money(request.Amount); ValidDate(request.Date);
        var reference = Text(request.Reference, "Número de comprobante", 120);
        var fileName = Text(Path.GetFileName(request.FileName), "Nombre de archivo", 200);
        if (request.Content.Length == 0 || request.Content.Length > MaxFileSize) throw new BusinessException("Adjunte un PDF, PNG o JPEG de hasta 5 MB.");
        var data = request.Content;
        var contentType = data.AsSpan().StartsWith("%PDF-"u8) ? "application/pdf" :
            data.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ? "image/png" :
            data.AsSpan().StartsWith(new byte[] { 255, 216, 255 }) ? "image/jpeg" : null;
        if (contentType is null) throw new BusinessException("El contenido del archivo debe ser PDF, PNG o JPEG.");
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!(contentType == "application/pdf" && extension == ".pdf" || contentType == "image/png" && extension == ".png" || contentType == "image/jpeg" && extension is ".jpg" or ".jpeg"))
            throw new BusinessException("La extensión del archivo no coincide con su contenido.");
        var fund = await Fund(ct); var v = await Voucher(id, ct);
        if (v.Status != VoucherStatus.Delivered) throw new BusinessException("Solo puede rendir un vale entregado y abierto.", 409);
        if (v.Receipts.Sum(r => r.Amount) + request.Amount > v.Amount) throw new BusinessException("Los gastos superan el importe entregado.");
        if (v.Receipts.Any(r => r.Reference.Equals(reference, StringComparison.OrdinalIgnoreCase))) throw new BusinessException("Ese comprobante ya está registrado en el vale.", 409);
        store.AddReceipt(new Receipt { VoucherId = id, Date = request.Date, Reference = reference, Amount = request.Amount, FileName = fileName, ContentType = contentType, Content = data });
        Log(fund, "Comprobante", $"VC-{v.Number:D6}: {reference}", voucherId: v.Id);
        await store.Save(ct);
    }
    public async Task Close(Guid id, CloseVoucherRequest request, CancellationToken ct)
    {
        Money(request.Returned, true);
        var fund = await Fund(ct); var v = await Voucher(id, ct);
        if (v.Status != VoucherStatus.Delivered) throw new BusinessException("Solo puede cerrar un vale entregado.", 409);
        if (v.Receipts.Sum(r => r.Amount) + request.Returned != v.Amount) throw new BusinessException("Los comprobantes más la devolución deben ser iguales al importe entregado.");
        v.Status = VoucherStatus.Closed; v.ClosedAt = time.GetUtcNow(); v.Returned = request.Returned;
        Log(fund, "Cierre", $"Rendición cerrada VC-{v.Number:D6}", request.Returned, v.Id);
        await store.Save(ct);
    }
    public async Task<Receipt> Download(Guid id, CancellationToken ct) => await store.GetReceipt(id, ct) ?? throw new BusinessException("El comprobante no existe.", 404);
    public async Task<List<MovementView>> Movements(CancellationToken ct) => (await store.GetMovements(ct)).Select(m => new MovementView(m.Id, m.At, m.Type, m.Detail, m.VoucherId, m.Amount, m.Balance)).ToList();
    public async Task<ReportView> Report(DateOnly from, DateOnly to, CancellationToken ct)
    {
        ValidDate(from); ValidDate(to);
        if (from > to) throw new BusinessException("La fecha inicial debe ser anterior o igual a la final.");
        var fund = await Fund(ct);
        var vouchers = (await List(ct)).Where(v => v.Date >= from && v.Date <= to).ToList();
        return new(from, to, fund.Currency, vouchers, vouchers.Sum(v => v.Amount), vouchers.Sum(v => v.Spent), vouchers.Sum(v => v.Returned));
    }
    public async Task<byte[]> Export(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var report = await Report(from, to, ct);
        static string Cell(string value)
        {
            if (value.TrimStart().StartsWith('=') || value.TrimStart().StartsWith('+') || value.TrimStart().StartsWith('-') || value.TrimStart().StartsWith('@') || value.StartsWith('\t') || value.StartsWith('\r')) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        static string Num(decimal n) => n.ToString("0.00", CultureInfo.InvariantCulture);
        var csv = new StringBuilder("Numero,Fecha,Responsable,Concepto,Moneda,Importe,Estado,Gastos,Devolucion,Pendiente\r\n");
        foreach (var v in report.Vouchers)
            csv.AppendLine(string.Join(",", new[] { v.Number, v.Date.ToString("yyyy-MM-dd"), v.Responsible, v.Concept, report.Currency, Num(v.Amount), v.Status, Num(v.Spent), Num(v.Returned), Num(v.Pending) }.Select(Cell)));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }
}