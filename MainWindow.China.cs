using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ShiSha;

public partial class MainWindow
{
    private readonly ChinaRegionService chinaRegions = new(Path.Combine(AppContext.BaseDirectory, "data", "china-regions"));
    private bool regionUiUpdating;
    private bool regionSelectionChanged;
    private bool regionLoading;
    private int regionGeneration;
    private readonly CancellationTokenSource regionLifetime = new();
    private WeatherCity CurrentWeatherCity => preferences.CustomWeatherCity is WeatherCity city && WeatherLocations.ValidCustom(city, preferences.WeatherCityId)
        ? city : WeatherLocations.Find(preferences.WeatherCityId);

    private void SetChinaControls(string country)
    {
        ChinaRegionPanel.Visibility = country == "CN" ? Visibility.Visible : Visibility.Collapsed;
        WeatherCityLabel.Visibility = WeatherCityInput.Visibility = country == "CN" ? Visibility.Collapsed : Visibility.Visible;
        if (country != "CN") { regionGeneration++; return; }
        regionUiUpdating = true;
        ChinaProvinceInput.ItemsSource = ChinaRegionService.Provinces;
        ChinaProvinceInput.SelectedValue = preferences.ChinaProvinceCode;
        if (ChinaProvinceInput.SelectedItem == null) ChinaProvinceInput.SelectedValue = "110000";
        regionUiUpdating = false;
        if (ChinaProvinceInput.SelectedItem is ChinaRegion province) _ = LoadChinaProvince(province);
    }
    private void ChangeChinaProvince(object sender, SelectionChangedEventArgs e)
    {
        if (regionUiUpdating || ChinaProvinceInput.SelectedItem is not ChinaRegion province) return;
        regionSelectionChanged = true; _ = LoadChinaProvince(province);
    }
    private async Task LoadChinaProvince(ChinaRegion province)
    {
        var generation = ++regionGeneration;
        regionUiUpdating = true;
        ChinaCityInput.ItemsSource = new[] { province }; ChinaCityInput.SelectedItem = province;
        ChinaDistrictInput.ItemsSource = new[] { province }; ChinaDistrictInput.SelectedItem = province;
        regionUiUpdating = false;
        if (!weatherNetworking) return;
        regionLoading = true; ChinaRegionMessage.Text = T("地区加载中", "Loading regions");
        try
        {
            var children = await chinaRegions.ChildrenAsync(province, regionLifetime.Token);
            if (generation != regionGeneration || exiting) return;
            regionUiUpdating = true;
            if (children.Count > 0 && children.All(r => r.Level == "district"))
            {
                ChinaDistrictInput.ItemsSource = new[] { province }.Concat(children).ToList();
                ChinaDistrictInput.SelectedValue = preferences.ChinaDistrictCode;
                if (ChinaDistrictInput.SelectedItem == null) ChinaDistrictInput.SelectedItem = province;
            }
            else
            {
                ChinaCityInput.ItemsSource = new[] { province }.Concat(children).ToList();
                ChinaCityInput.SelectedValue = preferences.ChinaCityCode;
                if (ChinaCityInput.SelectedItem == null) ChinaCityInput.SelectedItem = province;
            }
            regionUiUpdating = false;
            regionLoading = false; ChinaRegionMessage.Text = T("可停在任意一级；天气按所选区域代表点查询。", "Choose any level; weather uses a representative point.");
            if (ChinaCityInput.SelectedItem is ChinaRegion city && city.Code != province.Code) await LoadChinaCity(city);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidDataException)
        {
            if (generation == regionGeneration && !exiting) { regionLoading = false; ChinaRegionMessage.Text = T("地区暂时加载不了，请稍后重试。", "Regions are unavailable. Please retry later."); }
        }
        finally { regionUiUpdating = false; }
        ToolPanel.InvalidateMeasure();
    }
    private void ChangeChinaCity(object sender, SelectionChangedEventArgs e)
    {
        if (regionUiUpdating || ChinaCityInput.SelectedItem is not ChinaRegion city) return;
        regionSelectionChanged = true; _ = LoadChinaCity(city);
    }
    private async Task LoadChinaCity(ChinaRegion city)
    {
        var generation = ++regionGeneration;
        regionUiUpdating = true;
        ChinaDistrictInput.ItemsSource = new[] { city }; ChinaDistrictInput.SelectedItem = city;
        regionUiUpdating = false;
        if (city.Level == "province" || !weatherNetworking) { regionLoading = false; return; }
        regionLoading = true; ChinaRegionMessage.Text = T("地区加载中", "Loading regions");
        try
        {
            var children = await chinaRegions.ChildrenAsync(city, regionLifetime.Token);
            if (generation != regionGeneration || exiting) return;
            regionUiUpdating = true;
            ChinaDistrictInput.ItemsSource = new[] { city }.Concat(children).ToList();
            ChinaDistrictInput.SelectedValue = preferences.ChinaDistrictCode;
            if (ChinaDistrictInput.SelectedItem == null) ChinaDistrictInput.SelectedItem = city;
            regionUiUpdating = false;
            regionLoading = false; ChinaRegionMessage.Text = T("可停在任意一级；天气按所选区域代表点查询。", "Choose any level; weather uses a representative point.");
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException or InvalidDataException)
        {
            if (generation == regionGeneration && !exiting) { regionLoading = false; ChinaRegionMessage.Text = T("地区暂时加载不了，请稍后重试。", "Regions are unavailable. Please retry later."); }
        }
        finally { regionUiUpdating = false; }
        ToolPanel.InvalidateMeasure();
    }
    private void ChangeChinaDistrict(object sender, SelectionChangedEventArgs e)
    { if (!regionUiUpdating) regionSelectionChanged = true; }
    private void RetryChinaRegions(object sender, RoutedEventArgs e)
    { if (ChinaProvinceInput.SelectedItem is ChinaRegion province) _ = LoadChinaProvince(province); }
    private WeatherCity SelectedChinaRegion()
    {
        var selected = (ChinaDistrictInput.SelectedItem as ChinaRegion) ?? (ChinaCityInput.SelectedItem as ChinaRegion) ?? (ChinaProvinceInput.SelectedItem as ChinaRegion)
            ?? ChinaRegionService.Provinces.First(r => r.Code == "110000");
        var parts = new[] { ChinaProvinceInput.SelectedItem as ChinaRegion, ChinaCityInput.SelectedItem as ChinaRegion, selected }.Where(r => r != null).Select(r => r!.Name).Distinct();
        return selected.ToWeatherCity(string.Join(" / ", parts));
    }
}
