param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'build'))
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not (Test-Path -LiteralPath $compiler)){throw 'Requires .NET Framework C# compiler on Windows x64.'}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$destination=Join-Path (Resolve-Path $OutputDirectory).Path '西农校园网助手.exe'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ "/out:$destination" "/win32icon:$PSScriptRoot\src\app.ico" "/win32manifest:$PSScriptRoot\src\app.manifest" "/resource:$PSScriptRoot\src\crest.png,NwafuConnect.crest.png" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Security.dll /r:Microsoft.CSharp.dll "$PSScriptRoot\src\Core.cs" "$PSScriptRoot\src\App.cs" "$PSScriptRoot\src\AssemblyInfo.cs"
if($LASTEXITCODE -ne 0){throw 'Build failed'}
Write-Output $destination
Write-Output 'Place the unmodified BitSrunLoginGo v1.6.8 Windows amd64 bitsrun.exe inside engine/ next to the built EXE.'



& $compiler /nologo /target:winexe /platform:anycpu /optimize+ "/out:$OutputDirectory\EarlyStart.exe" "/win32manifest:$PSScriptRoot\src\app.manifest" "$PSScriptRoot\src\EarlyStart.cs"
if($LASTEXITCODE -ne 0){throw 'Early startup launcher build failed'}
