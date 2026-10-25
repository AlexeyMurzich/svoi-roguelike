param([Parameter(Mandatory=$true)][string]$Executable)
$ErrorActionPreference='Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
 $target=Join-Path $PSScriptRoot 'outputs\RenderBenchmark.exe'
 New-Item -ItemType Directory -Path (Split-Path $target) -Force | Out-Null
 & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe /optimize+ /warnaserror+ ('/out:'+$target) /reference:System.Drawing.dll /reference:System.Windows.Forms.dll 'Tools\RenderBenchmark.cs'
 if($LASTEXITCODE -ne 0){throw 'Benchmark compilation failed'}
 & $target $Executable
 if($LASTEXITCODE -ne 0){throw 'Benchmark failed'}
} finally { Pop-Location }
