$ErrorActionPreference='Stop'
$exe=Join-Path $PSScriptRoot '西农校园网助手.exe'
$launcher=Join-Path $PSScriptRoot 'EarlyStart.exe'
if(!(Test-Path -LiteralPath $exe) -or !(Test-Path -LiteralPath $launcher)){throw 'Please extract the complete release package first.'}
$run='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if(!(Get-ItemProperty -LiteralPath $run -Name 'NWAFU-CampusConnect' -ErrorAction SilentlyContinue)){throw 'Enable startup in the application first.'}
$user=[Security.Principal.WindowsIdentity]::GetCurrent().Name
$action=New-ScheduledTaskAction -Execute $launcher -WorkingDirectory $PSScriptRoot
$trigger=New-ScheduledTaskTrigger -AtLogOn -User $user
$principal=New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
$settings=New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName 'NWAFU-CampusConnect-EarlyStart' -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
New-ItemProperty -LiteralPath $run -Name 'NWAFU-CampusConnect' -Value ('"'+$exe+'" --autostart') -PropertyType String -Force | Out-Null
Write-Output 'Fast logon startup enabled. The application startup switch still controls whether it runs.'
