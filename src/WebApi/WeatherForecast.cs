namespace Iacula.WebApi;

public sealed record class WeatherForecast
{
    public required DateOnly Date { get; init; }
    public required int TemperatureC { get; init; }
    public string? Summary { get; init; }
}
