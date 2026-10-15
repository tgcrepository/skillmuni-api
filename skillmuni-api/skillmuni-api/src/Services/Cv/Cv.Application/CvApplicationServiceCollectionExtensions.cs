using Cv.Application.Parsing;
using Cv.Application.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class CvApplicationServiceCollectionExtensions
{
    /// <summary>Use cases and the interpreter. Options are bound by the host.</summary>
    public static IServiceCollection AddCvApplication(this IServiceCollection services)
    {
        services.AddSingleton<ICvInterpreter, CvInterpreter>();
        services.AddScoped<ICvService, CvService>();
        services.AddScoped<ICvParseJob, CvParseJob>();
        return services;
    }
}
