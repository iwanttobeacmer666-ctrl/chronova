$ErrorActionPreference = 'Stop'
$taskExe = Join-Path $PSScriptRoot 'app\时砂.exe'
if (-not (Test-Path -LiteralPath $taskExe)) { throw '找不到应用程序，请保留完整 app 文件夹。' }
$taskDesktop = [Environment]::GetFolderPath('Desktop')
$taskLink = Join-Path $taskDesktop '时砂.lnk'
if (Test-Path -LiteralPath $taskLink) { throw '同名桌面快捷方式已存在，未覆盖。' }
$taskShell = New-Object -ComObject WScript.Shell
try {
    $taskShortcut = $taskShell.CreateShortcut($taskLink)
    try {
        $taskShortcut.TargetPath = $taskExe
        $taskShortcut.WorkingDirectory = Join-Path $PSScriptRoot 'app'
        $taskShortcut.IconLocation = (Join-Path $PSScriptRoot 'app\assets\时砂.ico') + ',0'
        $taskShortcut.Description = '时砂 · 北京时间与定时备忘录'
        $taskShortcut.Save()
    } finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($taskShortcut) }
} finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($taskShell) }
Write-Output $taskLink
