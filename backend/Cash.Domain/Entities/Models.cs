namespace Cash.Domain.Entities;

public enum VoucherStatus { Pending, Delivered, Closed }
public sealed class Fund
{
    public int Id { get; set; } = 1;
    public string Currency { get; set; } = "";
    public decimal Balance { get; set; }
    public int NextNumber { get; set; } = 1;
    public long Revision { get; set; }
}
public sealed class Voucher
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int Number { get; set; }
    public DateOnly Date { get; set; }
    public string Responsible { get; set; } = "";
    public string Concept { get; set; } = "";
    public decimal Amount { get; set; }
    public VoucherStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public decimal Returned { get; set; }
    public List<Receipt> Receipts { get; set; } = [];
}
public sealed class Receipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid VoucherId { get; set; }
    public DateOnly Date { get; set; }
    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];
}
public sealed class Movement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset At { get; set; }
    public string Type { get; set; } = "";
    public string Detail { get; set; } = "";
    public Guid? VoucherId { get; set; }
    public decimal Amount { get; set; }
    public decimal Balance { get; set; }
}