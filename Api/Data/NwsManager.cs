using Api.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using static Microsoft.AspNetCore.Http.TypedResults;
using static System.IO.File;
using static System.Text.Json.JsonSerializer;
using static System.TimeSpan;

namespace Api
{
    public class NwsManager(HttpClient httpClient, IMemoryCache cache)
    {
        private static int _forecastCount = 0;

        private readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public async Task<Zone[]?> GetZonesAsync()
            // To get the live zone data from NWS, uncomment the following code and comment out the return statement below
            //var response = await httpClient.GetAsync("https://api.weather.gov/zones?type=forecast");
            //response.EnsureSuccessStatusCode();
            //var content = await response.Content.ReadAsStringAsync();
            //return JsonSerializer.Deserialize<ZonesResponse>(content, options);

            => await cache.GetOrCreateAsync("zones", async x =>
            {
                if (x is null)
                {
                    return [];
                }

                x.AbsoluteExpirationRelativeToNow = FromHours(1);

                // Deserialize the zones.json file from the wwwroot folder
                var zonesJson = Open("wwwroot/zones.json", FileMode.Open);

                return zonesJson is null
                    ? []
                    : (await DeserializeAsync<ZonesResponse>(zonesJson, _options))?.Features?.Where(f => f.Properties?.ObservationStations?.Count > 0).Select(f => (Zone)f).Distinct().ToArray() ?? [];
            });

        public async Task<Forecast[]> GetForecastByZoneAsync(string zoneId)
        {
            // Create an exception every 5 calls to simulate and error for testing
            _forecastCount++;

            if (_forecastCount % 5 == 0)
            {
                throw new Exception("Random exception thrown by NwsManager.GetForecastAsync");
            }

            var response = await httpClient.GetAsync($"https://api.weather.gov/zones/forecast/{zoneId}/forecast");

            _ = response.EnsureSuccessStatusCode();

            return (await response.Content.ReadFromJsonAsync<ForecastResponse>(_options))?.Properties?.Periods?.Select(p => (Forecast)p).ToArray() ?? [];
        }
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    public static class NwsManagerExtensions
    {
        public static IServiceCollection AddNwsManager(this IServiceCollection services)
        {
            _ = services.AddHttpClient<Api.NwsManager>(x =>
            {
                x.BaseAddress = new Uri("https://api.weather.gov/");
                x.DefaultRequestHeaders.Add("User-Agent", "Microsoft - .NET Aspire Demo");
            });

            _ = services.AddMemoryCache();

            // Add default output caching
            _ = services.AddOutputCache(x => x.AddBasePolicy(y => y.Cache()));

            return services;
        }

        public static WebApplication? MapApiEndpoints(this WebApplication? app)
        {
            if (app is null)
            {
                return default;
            }

            _ = app.UseOutputCache();

            _ = app.MapGet("/zones", async (Api.NwsManager manager) => Ok(await manager.GetZonesAsync())).WithName("GetZones").CacheOutput(x => x.Expire(FromHours(1))).WithOpenApi();

            _ = app.MapGet("/forecast/{zoneId}", async Task<Results<Ok<Api.Forecast[]>, NotFound>> (Api.NwsManager manager, string zoneId) =>
            {
                try
                {
                    return Ok(await manager.GetForecastByZoneAsync(zoneId));
                }
                catch (HttpRequestException)
                {
                    return NotFound();
                }
            }).WithName("GetForecastByZone").CacheOutput(x => x.Expire(FromMinutes(15)).SetVaryByRouteValue("zoneId")).WithOpenApi();

            return app;
        }
    }
}
