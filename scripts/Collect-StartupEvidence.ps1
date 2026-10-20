param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'diagnostics'),[string]$LogDirectory=(Join-Path $env:LOCALAPPDATA 'NWAFU-CampusConnect'),[Nullable[datetime]]$LoginUtc=$null,[Nullable[datetime]]$NetworkReadyUtc=$null)
$ErrorActionPreference='Stop'
$boot=(Get-CimInstance Win32_OperatingSystem).LastBootUpTime.ToUniversalTime()
$items=@()
Get-ChildItem -LiteralPath $LogDirectory -Filter 'diagnostic-v2.log*' -File | ForEach-Object {
 foreach($line in Get-Content -LiteralPath $_.FullName){
  if($line -match '^(\S+) run=([a-f0-9]+) version=([\d.]+) ms=(\d+) stage=(\w+) gen=(\d+) elapsed=(\d+) result=(\w+)$'){
   $utc=[datetime]::Parse($Matches[1]).ToUniversalTime()
   if($utc -ge $boot){$items += [pscustomobject]@{Utc=$utc;Run=$Matches[2];Version=$Matches[3];Ms=[long]$Matches[4];Stage=$Matches[5];Generation=[long]$Matches[6];Elapsed=[long]$Matches[7];Result=$Matches[8]}}
  }
 }
}
$entry=$items | Where-Object Stage -eq 'entry' | Sort-Object Utc | Select-Object -Last 1
if(!$entry){throw 'No current-boot v1.3 diagnostic entry; do not use old logs as evidence.'}
$current=@($items | Where-Object Run -eq $entry.Run | Sort-Object Ms)
$processUtc=$entry.Utc.AddMilliseconds(-$entry.Ms)
$online=$current | Where-Object Stage -eq 'online' | Select-Object -First 1
$internet=$current | Where-Object { $_.Stage -eq 'internet' -and $_.Result -eq 'ok' } | Select-Object -First 1
$auth=$current | Where-Object { $_.Stage -eq 'auth' -and $_.Result -eq 'begin' } | Select-Object -First 1
$missing=@()
if(!$LoginUtc){$missing+='Reliable interactive logon timestamp'}
if(!$NetworkReadyUtc){$missing+='Reliable interface-address-and-route-ready timestamp (a profile-connected event is only a candidate)'}
if(!$internet){$missing+='Successful external connectivity verification'}
$elapsed=$null
$passed=$false
if($missing.Count -eq 0){
 $start=if($LoginUtc -gt $NetworkReadyUtc){$LoginUtc}else{$NetworkReadyUtc}
 if($LoginUtc -lt $boot -or $NetworkReadyUtc -lt $boot -or $internet.Utc -lt $start -or $internet.Utc -lt $processUtc){$missing+='Timestamp ordering/current-boot validation failed'}
 else{$elapsed=($internet.Utc-$start).TotalMilliseconds;$passed=$elapsed -le 5000}
}
$report=[ordered]@{Version=$entry.Version;RunId=$entry.Run;BootUtc=$boot;ProcessUtc=$processUtc;LoginUtc=$LoginUtc;NetworkReadyUtc=$NetworkReadyUtc;SchoolConfirmedUtc=if($online){$online.Utc}else{$null};InternetConfirmedUtc=if($internet){$internet.Utc}else{$null};AuthenticationAttempted=[bool]$auth;ApplicationToInternetMs=if($internet){$internet.Ms}else{$null};EndToEndMs=$elapsed;FiveSecondPass=$passed;MissingEvidence=$missing;Stages=$current}
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'startup-evidence.json'),($report | ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($true))
$report | Select-Object Version,ApplicationToInternetMs,EndToEndMs,FiveSecondPass,MissingEvidence
