param([string]$Suite='All')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$out=Join-Path $root 'build/tests'
New-Item -ItemType Directory -Force $out | Out-Null
$src=@(Get-ChildItem (Join-Path $root 'src') -Filter *.cs | ForEach-Object FullName)
$testFiles=@(Get-ChildItem $PSScriptRoot -Filter *.cs | Where-Object {$_.BaseName -in @('FastTests','DiagnosticsTests','TransportTests','ControllerTests','HostTests','StartupTests','InstallerTests','BenchmarkTests','Fakes')} | ForEach-Object FullName)
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe /main:FastTests /out:"$out/FastTests.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.Management.dll /r:Microsoft.CSharp.dll @src @testFiles
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& "$out/FastTests.exe" $Suite
exit $LASTEXITCODE
