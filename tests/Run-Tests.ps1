$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$compiler=(Join-Path C:\WINDOWS 'Microsoft.NET\Framework64\v4.0.30319\csc.exe')
& $compiler /nologo /target:exe /out:"$root\tests\Tests.exe" /r:System.Web.Extensions.dll /r:System.Security.dll /r:Microsoft.CSharp.dll "$root\src\Core.cs" "$root\tests\Tests.cs"
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& "$root\tests\Tests.exe"
exit $LASTEXITCODE


