using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using Identity.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Security;

public sealed class RandomCodeGenerator(IOptions<SecurityOptions> options, IHostEnvironment environment) : ICodeGenerator
{
    private const int TokenBytes = 32;

    public static bool AllowsFixedCode(IHostEnvironment environment) =>
        environment.IsDevelopment() || environment.IsEnvironment("Testing");

    public string NewCode()
    {
        var fixedCode = options.Value.DevelopmentFixedCode;
        if (!string.IsNullOrEmpty(fixedCode) && AllowsFixedCode(environment))
        {
            return fixedCode;
        }

        return RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    }

    public string NewToken() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
}
