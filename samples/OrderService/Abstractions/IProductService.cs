using DaprFx.Core;
using OrderService.Models;

namespace OrderService.Abstractions;

public interface IProductService
{
    [DaprInvoke("/api/products/{id}")]
    Task<ProductDto?> GetProductAsync(string id);
}
