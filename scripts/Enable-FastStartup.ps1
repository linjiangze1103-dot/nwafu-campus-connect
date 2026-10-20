$exe=Join-Path $env:LOCALAPPDATA 'Programs/NWAFU-CampusConnect/西农校园网助手.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Install the complete package first.'}
Start-Process -FilePath $exe -ArgumentList '--set-autostart','on' -WindowStyle Hidden -Wait
