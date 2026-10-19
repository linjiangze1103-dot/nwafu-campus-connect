$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
New-Item -ItemType Directory -Force "$root/build/tests" | Out-Null
$src=@(Get-ChildItem "$root/src" -Filter *.cs | ForEach-Object FullName)
$testSources=@('LayoutTests.cs','Fakes.cs','HostTests.cs','FastTests.cs') | ForEach-Object { Join-Path (Join-Path $root 'tests') $_ }
& $compiler /nologo /target:exe /main:LayoutTests "/out:$root/build/tests/LayoutTests.exe" "/win32manifest:$root/src/app.manifest" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Security.dll /r:System.Management.dll /r:Microsoft.CSharp.dll @src @testSources
if($LASTEXITCODE -ne 0){throw 'Layout compilation failed'}
& "$root/build/tests/LayoutTests.exe"
exit $LASTEXITCODE
