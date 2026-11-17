using Cash.Domain.Entities;
namespace Cash.Domain.Interfaces;
public interface ICashStore
{
    Task<Fund?> GetFund(CancellationToken ct);
    Task<List<Voucher>> GetVouchers(CancellationToken ct);
    Task<Voucher?> GetVoucher(Guid id, CancellationToken ct);
    Task<Receipt?> GetReceipt(Guid id, CancellationToken ct);
    Task<List<Movement>> GetMovements(CancellationToken ct);
    void AddFund(Fund fund);
    void AddVoucher(Voucher voucher);
    void AddReceipt(Receipt receipt);
    void AddMovement(Movement movement);
    Task Save(CancellationToken ct);
}