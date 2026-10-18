Unregister-ScheduledTask -TaskName 'NWAFU-CampusConnect-EarlyStart' -Confirm:$false -ErrorAction SilentlyContinue
Write-Output 'Fast logon task removed; standard app startup setting is unchanged.'
