using System.ComponentModel.DataAnnotations;
using Pharmacy.Api.Domain;

namespace Pharmacy.Api.Contracts;

/// <summary>
/// One row of the grid (FR-01, FR-02). Notes are deliberately absent: the brief asks for
/// every attribute <em>except</em> notes, and there is no reason to ship free text the
/// grid will never render.
/// </summary>
public sealed record MedicineListItemDto(
    Guid Id,
    string FullName,
    string Brand,
    DateOnly ExpiryDate,
    int DaysToExpiry,
    int Quantity,
    decimal Price,
    ExpiryStatus ExpiryStatus,
    StockStatus StockStatus,
    RowSeverity RowSeverity,
    int Version);

/// <summary>The full record, including notes. Returned from the detail and write endpoints.</summary>
public sealed record MedicineDto(
    Guid Id,
    string FullName,
    string? Notes,
    string Brand,
    DateOnly ExpiryDate,
    int DaysToExpiry,
    int Quantity,
    decimal Price,
    ExpiryStatus ExpiryStatus,
    StockStatus StockStatus,
    RowSeverity RowSeverity,
    int Version,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ModifiedUtc);

/// <summary>Payload for adding a medicine (FR-03) and for updating one.</summary>
public sealed class SaveMedicineRequest : IValidatableObject
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Full name is required.")]
    [StringLength(200, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required(ErrorMessage = "Expiry date is required.")]
    public DateOnly ExpiryDate { get; set; }

    [Range(0, 1_000_000, ErrorMessage = "Quantity must be between 0 and 1,000,000.")]
    public int Quantity { get; set; }

    [Range(0.0, 10_000_000.0, ErrorMessage = "Price must be zero or greater.")]
    public decimal Price { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Brand is required.")]
    [StringLength(120)]
    public string Brand { get; set; } = string.Empty;

    /// <summary>
    /// [Required] cannot catch a missing DateOnly - an absent field simply binds to
    /// 0001-01-01 - so the sanity check lives here instead.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpiryDate < new DateOnly(1900, 1, 1))
        {
            yield return new ValidationResult(
                "Expiry date is required and must be a real date.",
                [nameof(ExpiryDate)]);
        }

        if (decimal.Round(Price, 2, MidpointRounding.AwayFromZero) != Price)
        {
            yield return new ValidationResult(
                "Price must have at most two decimal places.",
                [nameof(Price)]);
        }
    }
}

/// <summary>Query string of the grid request: search (FR-07), sort and page.</summary>
public sealed class MedicineQuery
{
    /// <summary>Case-insensitive "contains" match on the medicine name.</summary>
    public string? Search { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 10;

    /// <summary>One of: name, brand, expiryDate, quantity, price, severity. Defaults to severity.</summary>
    public string SortBy { get; set; } = "severity";

    /// <summary>
    /// asc or desc. The default (severity, desc) puts the red rows at the top, which is what
    /// someone opening the stock screen actually wants to see first.
    /// </summary>
    public string SortDir { get; set; } = "desc";
}

/// <summary>Standard paged envelope used by every collection endpoint.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

/// <summary>Counts for the dashboard strip above the grid.</summary>
public sealed record InventorySummaryDto(
    int TotalMedicines,
    int ExpiredCount,
    int ExpiringSoonCount,
    int LowStockCount,
    int OutOfStockCount,
    decimal TotalStockValue);
