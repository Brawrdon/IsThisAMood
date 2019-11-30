using Alexa.NET.Request;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace IsThisAMood.Middlewares
{
    /// <summary>
    /// An ASP.NET Core Middleware for validating Alexa requests
    /// </summary>
    public class AlexaRequestValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AlexaRequestValidationMiddleware> _logger;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="next"></param>
        public AlexaRequestValidationMiddleware(RequestDelegate next, ILogger<AlexaRequestValidationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        /// <summary>
        /// Validate if all necessary parts for a valid request are available
        /// and pass them to the RequestsVerification tool
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task Invoke(HttpContext context)
        {
            // EnableBuffering so the body can be read without causing issues to the request pipeline
            context.Request.EnableBuffering();
            
            // Verify SignatureCertChainUrl is present
            context.Request.Headers.TryGetValue("SignatureCertChainUrl", out var signatureChainUrl);
            if (string.IsNullOrWhiteSpace(signatureChainUrl))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            Uri certUrl;
            try
            {
                certUrl = new Uri(signatureChainUrl);
            }
            catch
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            // Verify SignatureCertChainUrl is Signature
            context.Request.Headers.TryGetValue("Signature", out var signature);
            if (string.IsNullOrWhiteSpace(signature))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            var body = await new StreamReader(context.Request.Body).ReadToEndAsync();
            context.Request.Body.Position = 0;

            if (string.IsNullOrWhiteSpace(body))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            var valid = await RequestVerification.Verify(signature, certUrl, body);
            if (!valid)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            await _next(context);
        }
    }
    
    /// <summary>
    /// Middleware builder
    /// </summary>
    public static class AlexaRequestValidationMiddlewareExtension
    {
        /// <summary>
        /// Add AlexaRequestValidationMiddleware to the request pipeline
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseAlexaRequestValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<AlexaRequestValidationMiddleware>();
        }
    }
}

