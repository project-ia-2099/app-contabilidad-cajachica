using Cash.Application.Services;
using Cash.Domain.Entities;
using Cash.Domain.Interfaces;
using Cash.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace Cash.Infrastructure.Repositories;
public sealed class CashStore(AppDbContext db) : ICashStore
{
    public Task<Fund?> GetFund(CancellationToken ct) => db.Funds.SingleOrDefaultAsync(x => x.Id == 1, ct);
    public Task<List<Voucher>> GetVouchers(CancellationToken ct) => db.Vouchers.Include(x => x.Receipts).OrderByDescending(x => x.Number).ToListAsync(ct);
    public Task<Voucher?> GetVoucher(Guid id, CancellationToken ct) => db.Vouchers.Include(x => x.Receipts).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<Receipt?> GetReceipt(Guid id, CancellationToken ct) => db.Receipts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<List<Movement>> GetMovements(CancellationToken ct) => db.Movements.AsNoTracking().OrderByDescending(x => x.At).ToListAsync(ct);
    public void AddFund(Fund fund) => db.Funds.Add(fund);
    public void AddVoucher(Voucher voucher) => db.Vouchers.Add(voucher);
    public void AddReceipt(Receipt receipt) => db.Receipts.Add(receipt);
    public void AddMovement(Movement movement) => db.Movements.Add(movement);
    public async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new BusinessException("La caja fue modificada por otro usuario. Actualice e intente nuevamente.", 409); }
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { throw new BusinessException("El registro ya existe o fue creado por otro usuario. Actualice la vista.", 409); }
    }
}