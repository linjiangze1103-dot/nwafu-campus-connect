$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$compiler=(Join-Path C:\WINDOWS 'Microsoft.NET\Framework64\v4.0.30319\csc.exe')
& $compiler /nologo /target:exe /main:LayoutTests /out:"$root\tests\LayoutTests.exe" /win32manifest:"$root\src\app.manifest" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Security.dll /r:Microsoft.CSharp.dll "$root\src\Core.cs" "$root\src\App.cs" "$root\tests\LayoutTests.cs"
if($LASTEXITCODE -ne 0){throw 'Layout test compilation failed'}
& "$root\tests\LayoutTests.exe"
exit $LASTEXITCODE


