$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Push-Location -LiteralPath $projectRoot
try {
 $version=[regex]::Match((Get-Content (Join-Path $projectRoot 'GameVersion.cs') -Raw), 'Number = "([^"]+)"').Groups[1].Value
 $compilerPath=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
 & $compilerPath /nologo /target:exe /optimize+ /warn:4 /warnaserror+ /out:Svoi.Tests.exe ('/reference:Svoi-v'+$version+'.exe') /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Core.dll @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'Tests') -Filter '*.cs' | Sort-Object Name | ForEach-Object { 'Tests\'+$_.Name })
 if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
} finally { Pop-Location }
