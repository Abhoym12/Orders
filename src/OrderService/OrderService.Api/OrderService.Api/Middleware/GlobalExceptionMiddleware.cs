using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using OrderService.Engine.Exceptions;
using System.Net;
using System.Text.Json;

namespace OrderService.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var problemDetails = exception switch
        {
            ValidationException validationEx => CreateValidationProblemDetails(validationEx),
            OrderDomainException domainEx => CreateDomainProblemDetails(domainEx),
            UnauthorizedAccessException => CreateUnauthorizedProblemDetails(),
            _ => CreateInternalServerErrorProblemDetails(exception)
        };

        _logger.LogError(exception, "An error occurred: {Message}", exception.Message);

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = problemDetails.Status ?? (int)HttpStatusCode.InternalServerError;

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private static ProblemDetails CreateValidationProblemDetails(ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = (int)HttpStatusCode.BadRequest,
            Title = "Validation failed",
            Detail = "One or more validation errors occurred."
        };
    }

    private static ProblemDetails CreateDomainProblemDetails(OrderDomainException ex)
    {
        return new ProblemDetails
        {
            Status = (int)HttpStatusCode.BadRequest,
            Title = "Domain error",
            Detail = ex.Message
        };
    }

    private static ProblemDetails CreateUnauthorizedProblemDetails()
    {
        return new ProblemDetails
        {
            Status = (int)HttpStatusCode.Unauthorized,
            Title = "Unauthorized",
            Detail = "You are not authorized to perform this action."
        };
    }

    private static ProblemDetails CreateInternalServerErrorProblemDetails(Exception ex)
    {
        return new ProblemDetails
        {
            Status = (int)HttpStatusCode.InternalServerError,
            Title = "Internal server error",
            Detail = "An unexpected error occurred. Please try again later."
        };
    }
}
