using System.Text.Json;
using FlowBoard.Api.Exceptions;
using FlowBoard.Shared.DTOs.Common;

namespace FlowBoard.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
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
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        if (exception is AppException)
        {
            _logger.LogWarning(
                exception,
                "Application exception. TraceId: {TraceId}",
                context.TraceIdentifier);
        }
        else
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred. TraceId: {TraceId}",
                context.TraceIdentifier);
        }

        var response = exception switch
        {
            AppException appException => new ErrorResponse
            {
                Code = appException.ErrorCode,
                Message = appException.Message,
                TraceId = context.TraceIdentifier,
                Details = appException.Details is null
                    ? null
                    : JsonSerializer.SerializeToElement(appException.Details)
            },

            _ => new ErrorResponse
            {
                Code = "INTERNAL_SERVER_ERROR",
                Message = "An unexpected error occurred.",
                TraceId = context.TraceIdentifier
            }
        };

        var statusCode = exception is AppException app
            ? app.StatusCode
            : StatusCodes.Status500InternalServerError;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
}