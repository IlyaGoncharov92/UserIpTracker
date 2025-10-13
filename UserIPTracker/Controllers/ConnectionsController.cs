using Microsoft.AspNetCore.Mvc;
using UserIPTracker.Models;
using UserIPTracker.Services;

namespace UserIPTracker.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConnectionsController : ControllerBase
{
    private readonly ConnectionsProcessor _connectionsProcessor;
    private readonly ConnectionsRepository _connectionsRepository;

    public ConnectionsController(ConnectionsProcessor connectionsProcessor, ConnectionsRepository connectionsRepository)
    {
        _connectionsProcessor = connectionsProcessor;
        _connectionsRepository = connectionsRepository;
    }

    // GET: api/users/find-by-ip?ipPart=192.168
    [HttpGet("find-by-ip")]
    public async Task<ActionResult<List<long>>> FindUsersByIp([FromQuery] string ipPart)
    {
        if (string.IsNullOrWhiteSpace(ipPart))
            return BadRequest("ipPart не может быть пустым");
        
        var userIds = await _connectionsRepository.FindUsersByIpPartAsync(ipPart);

        return Ok(userIds);
    }
    
    // GET: api/users/{userId}/ips
    [HttpGet("{userId}/ips")]
    public async Task<ActionResult<List<string>>> GetUserIps(long userId)
    {
        var ips = await _connectionsRepository.GetUserIpsAsync(userId);
        return Ok(ips);
    }

    // GET: api/users/{userId}/last-connection
    [HttpGet("{userId}/last-connection")]
    public async Task<ActionResult<object>> GetLastConnection(long userId)
    {
        var (time, ip) = await _connectionsRepository.GetLastConnectionAsync(userId);
        if (time is null && ip is null)
            return NotFound();

        return Ok(new { Time = time, Ip = ip });
    }

    // GET: api/users/last-connection-by-ip?ip=192.168.0.1
    [HttpGet("last-connection-by-ip")]
    public async Task<ActionResult<object>> GetLastConnectionByIp([FromQuery] string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return BadRequest("ip не может быть пустым");

        var (time, userId) = await _connectionsRepository.GetLastConnectionByIpAsync(ip);
        if (time is null && userId is null)
            return NotFound();

        return Ok(new { Time = time, UserId = userId });
    }
    
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ConnectionDto dto)
    {
        await _connectionsProcessor.WriteAsync(dto);
        return Accepted();
    }

    
    
    
    
    // ------- Для теста
    
    [HttpPost("count")]
    public async Task<IActionResult> Count()
    {
        int count = await _connectionsRepository.Count();
        return Accepted(count);
    }
    
    [HttpPost("clear")]
    public async Task<IActionResult> Clear()
    {
        await _connectionsRepository.TruncateAsync();
        return Accepted();
    }
}