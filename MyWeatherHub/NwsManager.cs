using System.Text.Json;
using static System.Text.Json.JsonSerializer;

namespace MyWeatherHub;

public class NwsManager(HttpClient client)
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Zone[]> GetZonesAsync()
    {
        var response = await client.GetAsync("zones");

        _ = response.EnsureSuccessStatusCode();

        return Deserialize<Zone[]>(await response.Content.ReadAsStringAsync(), _options) ?? [];
    }

    public async Task<Forecast[]> GetForecastByZoneAsync(string zoneId)
    {
        var response = await client.GetAsync($"forecast/{zoneId}");

        _ = response.EnsureSuccessStatusCode();

        return Deserialize<Forecast[]>(await response.Content.ReadAsStringAsync(), _options) ?? [];
    }
}

public record Zone(string Key, string Name, string State);

public record Forecast(string Name, string DetailedForecast);
