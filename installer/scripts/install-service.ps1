$ErrorActionPreference = 'Stop'

$serviceName = 'ConfigForgeHub'
$exePath = Join-Path $PSScriptRoot 'ConfigForge.Hub.Web.exe'

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 1
}

New-Service -Name $serviceName `
    -BinaryPathName $exePath `
    -DisplayName 'ConfigForge Hub' `
    -Description 'Aggregated configuration dashboard for connected ConfigForge instances.' `
    -StartupType Automatic

Start-Service -Name $serviceName
