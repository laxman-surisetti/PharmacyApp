namespace Pharmacy.Api.Domain;

/// <summary>
/// The medicine aggregate. This is the shape that is persisted to medicines.json.
/// </summary>
public sealed class Medicine
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Full name of the medicine, e.g. "Amoxicillin 500mg Capsule".</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Free text notes. Deliberately excluded from the grid projection.</summary>
    public string? Notes { get; set; }

    /// <summary>Expiry date. Date only - a medicine does not expire at a time of day.</summary>
    public DateOnly ExpiryDate { get; set; }

    /// <summary>Units currently on hand.</summary>
    public int Quantity { get; set; }

    /// <summary>Unit price, always stored rounded to 2 decimal places.</summary>
    public decimal Price { get; set; }

    public string Brand { get; set; } = string.Empty;

    /// <summary>Incremented on every write. Used for optimistic concurrency and cheap change detection.</summary>
    public int Version { get; set; } = 1;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ModifiedUtc { get; set; } = DateTimeOffset.UtcNow;
}
