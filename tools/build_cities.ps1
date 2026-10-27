$ErrorActionPreference = 'Stop'
$taskQueries = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'city-queries.json') -Raw | ConvertFrom-Json
$taskTarget = Join-Path (Split-Path $PSScriptRoot -Parent) 'Cities.json'
$taskExisting = if (Test-Path -LiteralPath $taskTarget) { @(Get-Content -LiteralPath $taskTarget -Raw | ConvertFrom-Json) } else { @() }
$taskPending = $taskQueries | Where-Object { ($_[0] + '-' + $_[1].Replace(' ','')) -notin $taskExisting.Id }
$taskFetched = @($taskPending | ForEach-Object -Parallel {
    $taskQuery = $_
    $taskCountry = $taskQuery[0]
    $taskName = $taskQuery[1]
    $taskUri = 'https://geocoding-api.open-meteo.com/v1/search?name=' + [Uri]::EscapeDataString($taskName) + '&countryCode=' + $taskCountry + '&count=1&language=en'
    for ($taskAttempt = 0; $taskAttempt -lt 2; $taskAttempt++) {
        try {
            $taskResult = Invoke-RestMethod -Uri $taskUri -TimeoutSec 8 -ErrorAction Stop
            $taskCity = $taskResult.results | Where-Object { $_.country_code -eq $taskCountry -and $_.feature_code.StartsWith('P') } | Select-Object -First 1
            if (-not $taskCity) { throw "No confirmed city" }
            [pscustomobject]@{Id=($taskCountry + '-' + $taskName.Replace(' ',''));CountryCode=$taskCountry;Name=$taskName;ChineseName=$taskQuery[2];Latitude=$taskCity.latitude;Longitude=$taskCity.longitude;TimeZone=$taskCity.timezone;GeoNamesId=$taskCity.id}
            break
        } catch { if ($taskAttempt -eq 1) { Write-Warning ("Unavailable: $taskCountry $taskName") } }
    }
} -ThrottleLimit 4)
$taskRows = @($taskExisting) + $taskFetched
$taskRows | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $taskTarget -Encoding utf8
Write-Output ('Verified ' + @($taskRows).Count + ' cities in ' + @($taskRows.CountryCode | Select-Object -Unique).Count + ' countries')
if (@($taskRows).Count -ne @($taskQueries).Count) { throw 'Some city lookups failed; confirmed entries saved. Run again to retry missing cities.' }
