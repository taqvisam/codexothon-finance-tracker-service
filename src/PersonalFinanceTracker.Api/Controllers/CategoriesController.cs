using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PersonalFinanceTracker.Application.DTOs.Categories;
using PersonalFinanceTracker.Application.Interfaces;

namespace PersonalFinanceTracker.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/categories")]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] Guid? accountId, [FromQuery] bool editableOnly, CancellationToken ct)
        => Ok(await categoryService.GetAllAsync(User.GetUserId(), accountId, editableOnly, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CategoryRequest request, CancellationToken ct)
        => Ok(await categoryService.CreateAsync(User.GetUserId(), request, ct));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CategoryRequest request, CancellationToken ct)
        => Ok(await categoryService.UpdateAsync(User.GetUserId(), id, request, ct));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await categoryService.DeleteAsync(User.GetUserId(), id, ct);
        return NoContent();
    }
}
