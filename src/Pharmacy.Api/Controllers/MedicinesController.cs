using Microsoft.AspNetCore.Mvc;
using Pharmacy.Api.Contracts;
using Pharmacy.Api.Services;

namespace Pharmacy.Api.Controllers;

/// <summary>The medicine catalogue: the grid, the detail record and the add/edit endpoints.</summary>
[ApiController]
[Route("api/v1/medicines")]
[Produces("application/json")]
public sealed class MedicinesController : ControllerBase
{
    private readonly IMedicineService _medicines;

    public MedicinesController(IMedicineService medicines)
    {
        _medicines = medicines;
    }

    /// <summary>
    /// FR-01, FR-02, FR-07. Returns one page of the grid. Each row carries the server-computed
    /// expiry / stock status and the colour band the UI should paint, so the rule is not
    /// duplicated in the client.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MedicineListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MedicineListItemDto>>> Search(
        [FromQuery] MedicineQuery query,
        CancellationToken cancellationToken)
        => Ok(await _medicines.SearchAsync(query, cancellationToken));

    /// <summary>Counts behind the dashboard tiles above the grid.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(InventorySummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InventorySummaryDto>> Summary(CancellationToken cancellationToken)
        => Ok(await _medicines.GetSummaryAsync(cancellationToken));

    /// <summary>The full record for one medicine, notes included.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MedicineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MedicineDto>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _medicines.GetByIdAsync(id, cancellationToken));

    /// <summary>FR-03. Adds a medicine to the catalogue.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MedicineDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MedicineDto>> Create(
        [FromBody] SaveMedicineRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _medicines.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>Updates an existing medicine.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(MedicineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MedicineDto>> Update(
        Guid id,
        [FromBody] SaveMedicineRequest request,
        CancellationToken cancellationToken)
        => Ok(await _medicines.UpdateAsync(id, request, cancellationToken));

    /// <summary>Removes a medicine from the catalogue. Sales already recorded are unaffected.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _medicines.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
