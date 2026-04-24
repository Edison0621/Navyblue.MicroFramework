using Microsoft.AspNetCore.Mvc;
using ProductService.Models;

namespace ProductService.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController : ControllerBase
{
    [HttpGet("{id}")]
    public ActionResult<ProductDto> GetById(string id)
    {
        var product = new ProductDto
        {
            Id = id,
            Name = $"Product-{id}",
            Price = 99.0m,
            InStock = true
        };

        return Ok(product);
    }
}
