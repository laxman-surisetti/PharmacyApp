namespace Pharmacy.Api.Domain;

/// <summary>
/// A sale transaction. Persisted to sales.json. Sales are append-only: they are never
/// edited or deleted, because a receipt that changes after the fact is not a receipt.
/// </summary>
public sealed class Sale
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Human readable reference, e.g. "SL-2026-000418".</summary>
    public string SaleNumber { get; set; } = string.Empty;

    public DateTimeOffset SoldAtUtc { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Who served the counter. No identity provider in this release, so free text.</summary>
    public string? SoldBy { get; set; }

    public string? Notes { get; set; }

    public List<SaleLine> Lines { get; set; } = new();

    /// <summary>Sum of the line totals, rounded to 2 decimals.</summary>
    public decimal TotalAmount { get; set; }
}

/// <summary>
/// One line of a sale. The medicine name and unit price are <em>copied</em> onto the line
/// rather than looked up later, so that a historic receipt does not change when the
/// catalogue is renamed or repriced.
/// </summary>
public sealed class SaleLine
{
    public Guid MedicineId { get; set; }

    public string MedicineName { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}
