$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$compiler=(Join-Path C:\WINDOWS 'Microsoft.NET\Framework64\v4.0.30319\csc.exe')
& $compiler /nologo /target:exe /main:UiTests /out:"$root\tests\UiTests.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.Security.dll /r:Microsoft.CSharp.dll "$root\src\Core.cs" "$root\src\App.cs" "$root\tests\UiTests.cs"
if($LASTEXITCODE -ne 0){throw 'GUI test compilation failed'}
& "$root\tests\UiTests.exe"
exit $LASTEXITCODE


