param([string]$OutputDirectory=(Join-Path (Split-Path $PSScriptRoot -Parent) 'dist'),[string]$DependencyDirectory='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$OutputDirectory=(Resolve-Path $OutputDirectory).Path
$package=Join-Path $OutputDirectory 'NWAFU-CampusConnect-1.3.0-Windows-x64'
if(Test-Path -LiteralPath $package){throw 'Package exists; select a fresh output directory.'}
New-Item -ItemType Directory $package | Out-Null
& (Join-Path $root 'Build.ps1') -OutputDirectory $package
if($LASTEXITCODE -ne 0){throw 'Build failed'}
$engine=Join-Path $package 'engine'
$third=Join-Path $package 'third-party'
New-Item -ItemType Directory $engine,$third | Out-Null
$sourceZip=Join-Path $third 'BitSrunLoginGo-v1.6.8-source.zip'
if($DependencyDirectory){
 Copy-Item -LiteralPath (Join-Path $DependencyDirectory 'engine/bitsrun.exe') -Destination $engine
 Copy-Item -LiteralPath (Join-Path $DependencyDirectory 'third-party/BitSrunLoginGo-v1.6.8-source.zip') -Destination $sourceZip
}else{
 $archive=Join-Path $OutputDirectory 'bitsrun_windows_amd64.tar.gz'
 Invoke-WebRequest -UseBasicParsing 'https://github.com/Mmx233/BitSrunLoginGo/releases/download/v1.6.8/bitsrun_windows_amd64.tar.gz' -OutFile $archive
 if((Get-FileHash $archive).Hash -ne 'EC98F739FF7884096B6CA2E0B91AFB4E2A9FB6D81D203C35E51E3C13334585A6'){throw 'Engine archive checksum mismatch'}
 & tar -xzf $archive -C $engine
 if($LASTEXITCODE -ne 0){throw 'Engine extraction failed'}
 Invoke-WebRequest -UseBasicParsing 'https://codeload.github.com/Mmx233/BitSrunLoginGo/zip/refs/tags/v1.6.8' -OutFile $sourceZip
}
if((Get-FileHash (Join-Path $engine 'bitsrun.exe')).Hash -ne '509E8842167A479A563F24D154D9D4536B2541B46F1231D13EE37A51A1107785'){throw 'Engine checksum mismatch'}
if((Get-FileHash $sourceZip).Hash -ne '7794F1E4993863CF033056DB4777C348D897FBD4B8724F7E743FF05FC8F94426'){throw 'Source checksum mismatch'}
$source=Join-Path $package 'source'
New-Item -ItemType Directory $source | Out-Null
Copy-Item (Join-Path $root 'src') $source -Recurse
New-Item -ItemType Directory (Join-Path $source 'tests') | Out-Null
Get-ChildItem (Join-Path $root 'tests') -File | Where-Object {$_.Extension -in @('.cs','.ps1')} | Copy-Item -Destination (Join-Path $source 'tests')
Copy-Item (Join-Path $root 'Build.ps1') $source
Copy-Item (Join-Path $root 'scripts') $source -Recurse
foreach($name in @('README.md','LICENSE','THIRD-PARTY-NOTICES.txt')){Copy-Item (Join-Path $root $name) $package;Copy-Item (Join-Path $root $name) $source}
foreach($name in @('Enable-FastStartup.ps1','Disable-FastStartup.ps1','Collect-StartupEvidence.ps1')){Copy-Item (Join-Path $root ('scripts/'+$name)) $package}
$files=Get-ChildItem -LiteralPath $package -File -Recurse
if($files | Where-Object {$_.Name -match '^(account\.dat|settings\.json|credential\.xml|runtime-.*\.json)$' -or $_.Extension -eq '.log'}){throw 'Private runtime file in package'}
$hashes=$files | Sort-Object FullName | ForEach-Object {(Get-FileHash -LiteralPath $_.FullName).Hash.ToLower()+'  '+$_.FullName.Substring($package.Length+1).Replace('\','/')}
[IO.File]::WriteAllLines((Join-Path $package 'SHA256SUMS.txt'),[string[]]$hashes,[Text.UTF8Encoding]::new($false))
$zip=Join-Path $OutputDirectory 'NWAFU-CampusConnect-1.3.0-Windows-x64.zip'
Compress-Archive -LiteralPath $package -DestinationPath $zip
Get-FileHash -LiteralPath $zip
