using Asp.Versioning;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using SkillMuni.Common.Tenancy;
using SkillMuni.Web.Correlation;
using SkillMuni.Web.Errors;
using SkillMuni.Web.Tenancy;
using SkillMuni.Web.Validation;

namespace SkillMuni.Web.Hosting;

/// <summary>The plumbing every SkillMuni service shares, so each Program.cs stays a few lines.</summary>
public static class WebDefaults
{
    public const string ReadyTag = "ready";

    /// <summary>Key Vault, telemetry, clock, correlation, tenancy, auth and error handling.</summary>
    public static WebApplicationBuilder AddSkillMuniPlatform(this WebApplicationBuilder builder, string serviceName)
    {
        AddKeyVault(builder);
        AddTelemetry(builder, serviceName);

        var services = builder.Services;
        services.AddSkillMuniCommon();
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationContext, HttpCorrelationContext>();
        services.AddOptions<TenancyOptions>().BindConfiguration(TenancyOptions.Section);
        services.AddScoped<TenantOverride>();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddSkillMuniAuthentication();
        services.AddHealthChecks();
        return builder;
    }

    /// <summary><see cref="AddSkillMuniPlatform"/> plus controllers, URL versioning, validation and OpenAPI.</summary>
    public static WebApplicationBuilder AddSkillMuniServiceDefaults(this WebApplicationBuilder builder, string serviceName)
    {
        builder.AddSkillMuniPlatform(serviceName);
        var services = builder.Services;

        services.AddControllers(o => o.Filters.Add<ValidationFilter>());
        services.Configure<ApiBehaviorOptions>(o => o.InvalidModelStateResponseFactory = context =>
        {
            var fields = context.ModelState
                .Where(kv => kv.Value is { Errors.Count: > 0 })
                .GroupBy(kv => ValidationFilter.FieldName(kv.Key.TrimStart('$', '.')))
                .ToDictionary(g => g.Key, _ => new[] { "This field is missing or invalid." });
            var problem = ApiProblems.Create(
                context.HttpContext,
                StatusCodes.Status400BadRequest,
                "malformed-request",
                "The request could not be read.",
                fields);
            return ApiProblems.ToResult(problem);
        });

        services.AddApiVersioning(o =>
            {
                o.DefaultApiVersion = new ApiVersion(1, 0);
                o.AssumeDefaultVersionWhenUnspecified = true;
                o.ReportApiVersions = true;
                o.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(o =>
            {
                o.GroupNameFormat = "'v'V";
                o.SubstituteApiVersionInUrl = true;
            });

        // Built-in OpenAPI (.NET 10): the "v1" document picks up the API-explorer group "v1".
        services.AddOpenApi("v1");
        return builder;
    }

    public static WebApplication UseSkillMuniServiceDefaults(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();

        // Each service publishes /openapi/v1.json; the gateway serves them together (development only).
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        app.UseAuthentication();
        app.UseMiddleware<TenantGuardMiddleware>();
        app.UseAuthorization();
        app.MapSkillMuniHealth();
        app.MapControllers();
        return app;
    }

    /// <summary><c>/health/live</c> (process up) and <c>/health/ready</c> (dependencies tagged "ready").</summary>
    public static IEndpointRouteBuilder MapSkillMuniHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadyTag) }).AllowAnonymous();
        return endpoints;
    }

    private static void AddKeyVault(WebApplicationBuilder builder)
    {
        // Secrets come from Key Vault through the app's managed identity, never from config files (CLAUDE.md §6).
        var vault = builder.Configuration["KeyVault:Uri"];
        if (!string.IsNullOrWhiteSpace(vault))
        {
            builder.Configuration.AddAzureKeyVault(new Uri(vault), new DefaultAzureCredential());
        }
    }

    private static void AddTelemetry(WebApplicationBuilder builder, string serviceName)
    {
        var otel = builder.Services.AddOpenTelemetry();
        otel.ConfigureResource(r => r.AddService($"skillmuni-{serviceName}"));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            otel.UseAzureMonitor();
            return;
        }

        otel.WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());
        otel.WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation());
    }
}
