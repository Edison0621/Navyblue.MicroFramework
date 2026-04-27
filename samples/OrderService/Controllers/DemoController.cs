using Microsoft.AspNetCore.Mvc;
using OrderService.Models;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("demo")]
public sealed class DemoController(IConfiguration configuration, EventAuditStore eventAuditStore) : ControllerBase
{
    private readonly IConfiguration _configuration = configuration;
    private readonly EventAuditStore _eventAuditStore = eventAuditStore;

    [HttpGet("events")]
    public IActionResult Events([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var query = new PageQuery(page, pageSize);
        var items = _eventAuditStore.List(query.SafePage * query.SafePageSize).ToList();
        var total = items.Count;
        var paged = items.Skip((query.SafePage - 1) * query.SafePageSize).Take(query.SafePageSize).ToList();
        var data = new PagedResult<object>(paged, query.SafePage, query.SafePageSize, total);
        return Ok(new ApiResponse<PagedResult<object>>(true, data, null));
    }

    [HttpGet("config")]
    public IActionResult Config()
        => Ok(new ApiResponse<object>(true, new
        {
            RefreshSample = _configuration["Demo:RefreshSample"],
            FailOrderSubscriber = _configuration.GetValue<bool>("Demo:FailOrderSubscriber"),
            OtlpEndpoint = _configuration["Demo:OtlpEndpoint"] ?? "http://localhost:4317",
            UtcNow = DateTimeOffset.UtcNow
        }, null));
}
