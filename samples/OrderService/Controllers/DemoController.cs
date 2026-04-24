using Microsoft.AspNetCore.Mvc;
using OrderService.Services;

namespace OrderService.Controllers;

[ApiController]
[Route("demo")]
public sealed class DemoController(IConfiguration configuration, EventAuditStore eventAuditStore) : ControllerBase
{
    private readonly IConfiguration _configuration = configuration;
    private readonly EventAuditStore _eventAuditStore = eventAuditStore;

    [HttpGet("events")]
    public IActionResult Events([FromQuery] int take = 50)
        => Ok(_eventAuditStore.List(take));

    [HttpGet("config")]
    public IActionResult Config()
        => Ok(new
        {
            RefreshSample = _configuration["Demo:RefreshSample"],
            FailOrderSubscriber = _configuration.GetValue<bool>("Demo:FailOrderSubscriber"),
            OtlpEndpoint = _configuration["Demo:OtlpEndpoint"] ?? "http://localhost:4317",
            UtcNow = DateTimeOffset.UtcNow
        });
}
