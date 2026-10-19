param([string]$OutputDirectory=(Join-Path $PSScriptRoot 'build'))
$ErrorActionPreference='Stop'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$OutputDirectory=(Resolve-Path $OutputDirectory).Path
$src=@(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter *.cs | Where-Object {$_.Name -ne 'EarlyStart.cs'} | ForEach-Object FullName)
$refs=@('/r:System.Windows.Forms.dll','/r:System.Drawing.dll','/r:System.Web.Extensions.dll','/r:System.Security.dll','/r:System.Management.dll','/r:Microsoft.CSharp.dll')
& $compiler /nologo /target:winexe /main:NwafuConnect.Program /platform:anycpu /optimize+ "/out:$OutputDirectory/西农校园网助手.exe" "/win32icon:$PSScriptRoot/src/app.ico" "/win32manifest:$PSScriptRoot/src/app.manifest" "/resource:$PSScriptRoot/src/crest.png,NwafuConnect.crest.png" @refs @src
if($LASTEXITCODE -ne 0){throw 'Build failed'}
if(Test-Path (Join-Path $PSScriptRoot 'src/Installer.cs')){
 & $compiler /nologo /target:winexe /main:NwafuConnect.InstallerProgram /optimize+ "/out:$OutputDirectory/安装或更新.exe" "/win32manifest:$PSScriptRoot/src/app.manifest" @refs @src
 if($LASTEXITCODE -ne 0){throw 'Installer build failed'}
}
Get-ChildItem $OutputDirectory -Filter *.exe | Select-Object Name,Length
