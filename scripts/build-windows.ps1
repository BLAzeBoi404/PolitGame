$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '..')
dotnet build Sovereign.csproj -c Release --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
New-Item -ItemType Directory -Force artifacts | Out-Null
$exe = Join-Path (Get-Location) 'bin/Release/net472/Sovereign.exe'
$test = Start-Process -FilePath $exe -ArgumentList @('--test', 'artifacts/tests-windows.txt') -Wait -PassThru
if ($test.ExitCode -ne 0) { throw "Game tests failed: $($test.ExitCode)" }
Get-Content artifacts/tests-windows.txt
