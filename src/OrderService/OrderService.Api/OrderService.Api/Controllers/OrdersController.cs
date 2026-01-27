using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Manager.Commands;
using OrderService.Manager.Queries;
using Shared.Contracts.Dtos;

namespace OrderService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(IMediator mediator, ILogger<OrdersController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var command = new CreateOrderCommand(userId, request.Items);
        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { id = result.OrderId }, result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderResponse>> GetOrder(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = new GetOrderByIdQuery(id);
        var result = await _mediator.Send(query, cancellationToken);

        if (result == null)
        {
            return NotFound(new ProblemDetails
            {
                Status = 404,
                Title = "Order not found",
                Detail = $"Order with ID {id} was not found."
            });
        }

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<List<OrderResponse>>> ListOrders(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var query = new ListOrdersQuery(userId, status);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPut("{id:guid}/cancel")]
    public async Task<ActionResult<OrderResponse>> CancelOrder(
        Guid id,
        [FromBody] CancelOrderRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var command = new CancelOrderCommand(id, userId, request.Reason);
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    private Guid GetUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Unable to extract user ID from claims");
            throw new UnauthorizedAccessException("Unable to identify user.");
        }

        return userId;
    }
}
