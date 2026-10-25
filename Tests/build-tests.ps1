$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
Push-Location -LiteralPath $projectRoot
try {
 $compilerPath=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
 & $compilerPath /nologo /target:exe /optimize+ /warn:4 /warnaserror+ /out:Svoi.Tests.exe /reference:Svoi-v0.11.exe /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Core.dll 'Tests\TestSuite.cs' 'Tests\StartingBuffTests.cs' 'Tests\UiTests.cs' 'Tests\MapTests.cs' 'Tests\MapMenuTests.cs' 'Tests\FullscreenTests.cs' 'Tests\HudTests.cs' 'Tests\CombatTests.cs' 'Tests\CombatUiTests.cs' 'Tests\MotionTests.cs' 'Tests\DogTests.cs' 'Tests\ReviewTests.cs' 'Tests\AnimationRenderTests.cs' 'Tests\DeathTests.cs'
 if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
} finally { Pop-Location }
