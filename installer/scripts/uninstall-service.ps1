$ErrorActionPreference = 'SilentlyContinue'

$serviceName = 'ConfigForgeHub'

if (Get-Service -Name $serviceName -ErrorAction SilentlyContinue) {
    Stop-Service -Name $serviceName -Force
    & sc.exe delete $serviceName | Out-Null
}
