
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using RideMatching.Api.DTOs;

namespace RideMatching.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
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
            _logger.LogError(
                ex,
                "Unhandled exception. TraceId: {TraceId}",
                context.TraceIdentifier);

            if (context.Response.HasStarted)
            {
                throw;
            }

            var (statusCode, message) = ex switch
            {
                KeyNotFoundException =>
                    (StatusCodes.Status404NotFound, "Resource not found."),

                ValidationException =>
                    (StatusCodes.Status400BadRequest, "Invalid request."),

                ArgumentException =>
                    (StatusCodes.Status400BadRequest, "Invalid request."),

                _ =>
                    (StatusCodes.Status500InternalServerError,
                     "An unexpected error occurred.")
            };

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var response = new ApiErrorResponse
            {
                StatusCode = statusCode,
                Message = message,
                TraceId = context.TraceIdentifier
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
