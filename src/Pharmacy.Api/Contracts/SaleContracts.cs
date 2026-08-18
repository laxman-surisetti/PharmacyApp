using System.ComponentModel.DataAnnotations;

namespace Pharmacy.Api.Contracts;

/// <summary>Request to record a sale (FR-06).</summary>
public sealed class CreateSaleRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "A sale must have at least one line.")]
    public List<CreateSaleLineRequest> Lines { get; set; } = new();

    [StringLength(120)]
    public string? SoldBy { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}

public sealed class CreateSaleLineRequest
{
    [Required]
    public Guid MedicineId { get; set; }

    [Range(1, 100_000, ErrorMessage = "Quantity sold must be at least 1.")]
    public int Quantity { get; set; }
}

public sealed record SaleDto(
    Guid Id,
    string SaleNumber,
    DateTimeOffset SoldAtUtc,
    string? SoldBy,
    string? Notes,
    IReadOnlyList<SaleLineDto> Lines,
    decimal TotalAmount);

public sealed record SaleLineDto(
    Guid MedicineId,
    string MedicineName,
    string Brand,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>Query string for the sales history list.</summary>
public sealed class SaleQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 10;

    /// <summary>Optional inclusive lower bound on the sale date.</summary>
    public DateOnly? From { get; set; }

    /// <summary>Optional inclusive upper bound on the sale date.</summary>
    public DateOnly? To { get; set; }
}
