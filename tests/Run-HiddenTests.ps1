$ErrorActionPreference='Stop'
$r=Split-Path $PSScriptRoot -Parent
& (Join-Path C:\WINDOWS 'Microsoft.NET\Framework64\v4.0.30319\csc.exe') /nologo /target:exe /main:HiddenTests "/out:$r\tests\HiddenTests.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Security.dll /r:Microsoft.CSharp.dll "$r\src\Core.cs" "$r\src\App.cs" "$r\tests\HiddenTests.cs"
if($LASTEXITCODE -ne 0){throw 'Compilation failed'}
& "$r\tests\HiddenTests.exe"
exit $LASTEXITCODE

