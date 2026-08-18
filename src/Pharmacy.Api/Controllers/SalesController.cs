using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Contracts;
using Pharmacy.Api.Services;

namespace Pharmacy.Api.Controllers;

/// <summary>FR-06 - the sale record. Sales are append-only; they are never edited or deleted.</summary>
[ApiController]
[Route("api/v1/sales")]
[Produces("application/json")]
public sealed class SalesController : ControllerBase
{
    private readonly ISaleService _sales;

    public SalesController(ISaleService sales)
    {
        _sales = sales;
    }

    /// <summary>Sales history, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SaleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<SaleDto>>> Search(
        [FromQuery] SaleQuery query,
        CancellationToken cancellationToken)
        => Ok(await _sales.SearchAsync(query, cancellationToken));

    /// <summary>One sale, with its lines and the unit prices captured at the time of sale.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SaleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaleDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _sales.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// Records a sale and decrements stock in one operation. Rejected with 409 if any line
    /// asks for more units than are on hand.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SaleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SaleDto>> Create(
        [FromBody] CreateSaleRequest request,
        CancellationToken cancellationToken)
    {
        var sale = await _sales.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = sale.Id }, sale);
    }
}
