using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace ShiSha;

public partial class MainWindow
{
    private readonly WeatherService weatherService = new();
    private readonly bool weatherNetworking;
    private CancellationTokenSource? weatherRequest;
    private WeatherForecast? forecast;
    private bool weatherLoading;
    private bool weatherUnavailable;
    private DateTimeOffset nextWeatherRefresh = DateTimeOffset.MinValue;
    private string CityName(WeatherCity city) => Chinese ? city.ChineseName : city.Name;

    private void ApplyWeatherLanguage()
    {
        var city = CurrentWeatherCity;
        regionUiUpdating = true;
        WeatherCountryInput.ItemsSource = WeatherLocations.Countries.Select(c => new ZoneChoice(c.Id, T(c.ChineseName, c.EnglishName))).ToList();
        WeatherCountryInput.SelectedValue = city.CountryCode;
        regionUiUpdating = false;
        FillWeatherCities(city.CountryCode);
        SetChinaControls(city.CountryCode);
        AutomationProperties.SetName(WeatherCountryInput, T("天气国家", "Weather country"));
        AutomationProperties.SetName(WeatherCityInput, T("天气城市", "Weather city"));
        RenderWeather();
    }
    private void ChangeWeatherCountry(object sender, SelectionChangedEventArgs e)
    {
        if (regionUiUpdating) return;
        if (WeatherCountryInput.SelectedValue is string country) { FillWeatherCities(country); SetChinaControls(country); }
    }
    private void FillWeatherCities(string country)
    {
        var cities = WeatherLocations.Cities.Where(c => c.CountryCode == country).ToList();
        WeatherCityInput.ItemsSource = cities.Select(c => new ZoneChoice(c.Id, CityName(c))).ToList();
        WeatherCityInput.SelectedValue = cities.Any(c => c.Id == preferences.WeatherCityId) ? preferences.WeatherCityId : cities.FirstOrDefault()?.Id;
    }
    private async Task RefreshWeatherAsync()
    {
        if (!weatherNetworking || exiting) return;
        weatherRequest?.Cancel();
        var request = new CancellationTokenSource();
        weatherRequest = request;
        var city = CurrentWeatherCity;
        weatherLoading = true; weatherUnavailable = false; forecast = null;
        nextWeatherRefresh = DateTimeOffset.UtcNow.AddMinutes(15);
        RenderWeather();
        try
        {
            var result = await weatherService.FetchAsync(city, request.Token);
            if (exiting || !ReferenceEquals(weatherRequest, request)) return;
            forecast = result;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidDataException)
        {
            if (!ReferenceEquals(weatherRequest, request)) return;
            forecast = null; weatherUnavailable = true;
            nextWeatherRefresh = DateTimeOffset.UtcNow.AddMinutes(5);
        }
        finally
        {
            if (ReferenceEquals(weatherRequest, request))
            {
                weatherRequest = null; weatherLoading = false;
                if (!exiting) RenderWeather();
            }
            request.Dispose();
        }
    }
    private void RenderWeather()
    {
        var city = CurrentWeatherCity;
        WeatherCityName.Text = CityName(city); WeatherCityName.ToolTip = city.AdministrativePath ?? CityName(city);
        if (forecast == null)
        {
            WeatherIcon.Text = weatherUnavailable ? "…" : "◌";
            WeatherCondition.Text = weatherUnavailable ? T("天气暂时连不上，稍后再试。", "Weather is unavailable. Please try again later.") : T("天气加载中", "Loading weather");
            WeatherTemperature.Text = ""; WeatherWind.Text = "";
            WeatherCard.ToolTip = WeatherCondition.Text;
            return;
        }
        var condition = WeatherMath.Condition(forecast.Code, forecast.IsDay);
        WeatherIcon.Text = condition.Icon; WeatherCondition.Text = T(condition.Chinese, condition.English);
        var culture = CultureInfo.GetCultureInfo(preferences.LanguageCode);
        WeatherTemperature.Text = forecast.Low.ToString("0", culture) + "–" + forecast.High.ToString("0", culture) + "°C";
        var direction = WeatherMath.Direction(forecast.WindDirection);
        string chineseDirection = direction switch { "N" => "北风", "NE" => "东北风", "E" => "东风", "SE" => "东南风", "S" => "南风", "SW" => "西南风", "W" => "西风", _ => "西北风" };
        WeatherWind.Text = T(chineseDirection, direction) + " · " + WeatherMath.Beaufort(forecast.WindSpeed) + T("级", " Bft");
        WeatherCard.ToolTip = (city.AdministrativePath ?? CityName(city)) + "\n" + forecast.LocalDate + T(" · 当地今日温度范围", " · Today's local temperature range")
            + "\n" + WeatherCondition.Text + "  " + WeatherTemperature.Text + "\n" + WeatherWind.Text + " (" + forecast.WindSpeed.ToString("0.0", culture) + " m/s)"
            + (city.Id.StartsWith("CN-AD-", StringComparison.Ordinal) ? "\nOpen-Meteo / DataV" : "\nOpen-Meteo / GeoNames");
    }
    private void OpenWeatherSource(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        try { Process.Start(new ProcessStartInfo("https://open-meteo.com/") { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
    }
}
