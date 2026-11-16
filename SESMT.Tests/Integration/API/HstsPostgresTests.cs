using Microsoft.AspNetCore.Hosting;
using System.Net;

namespace SESMT.Tests.Integration.API;

[Collection(PostgresCollection.Name)]
public sealed class HstsPostgresTests(PostgresDatabaseFixture database)
{
    [PostgresFact]
    public async Task OutsideDevelopmentHttpsResponsesCarryHstsAndPlainHttpDoesNot()
    {
        var keys = Directory.CreateTempSubdirectory("sesmt-hsts-keys");
        try
        {
            await using var factory = new PostgresApiFactory(database);
            await using var staging = factory.WithWebHostBuilder(builder => builder.UseEnvironment("Staging")
                .UseSetting("DataProtection:KeysPath", keys.FullName));
            using var https = staging.CreateClient(new() { BaseAddress = new Uri("https://api.exemplo.test") });
            using var secure = await https.GetAsync("/health");
            Assert.Equal(HttpStatusCode.OK, secure.StatusCode);
            var header = Assert.Single(secure.Headers.GetValues("Strict-Transport-Security"));
            Assert.Contains("max-age=31536000", header); Assert.Contains("includeSubDomains", header);

            using var http = staging.CreateClient(new() { BaseAddress = new Uri("http://api.exemplo.test") });
            using var plain = await http.GetAsync("/health");
            Assert.False(plain.Headers.Contains("Strict-Transport-Security"));
        }
        finally { keys.Delete(true); }
    }
}
