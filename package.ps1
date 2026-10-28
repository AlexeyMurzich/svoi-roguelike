$ErrorActionPreference='Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
 $version=[regex]::Match((Get-Content (Join-Path $PSScriptRoot 'GameVersion.cs') -Raw), 'Number = "([^"]+)"').Groups[1].Value
 $release=Join-Path $PSScriptRoot ('outputs\Svoi-v'+$version)
 New-Item -ItemType Directory -Path $release -Force | Out-Null
 Copy-Item -LiteralPath ('Svoi-v'+$version+'.exe'),'README.txt' -Destination $release
 foreach($mode in @('babai','battle','dogs','hedgehog','rabid-hedgehog','hedgehogs','mimicrator','toha','illya-toha')) {
  $launcher='@echo off'+"`r`n"+'start "" "%~dp0Svoi-v'+$version+'.exe" --test-'+$mode+"`r`n"
  [IO.File]::WriteAllText((Join-Path $release ('Launch-test-'+$mode+'.cmd')),$launcher,[Text.Encoding]::ASCII)
 }
 $binary=Join-Path $release ('Svoi-v'+$version+'.exe')
 $hash=(Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash
 [IO.File]::WriteAllText((Join-Path $release 'SHA256.txt'),$hash+'  '+[IO.Path]::GetFileName($binary)+"`r`n",[Text.Encoding]::ASCII)
 Compress-Archive -LiteralPath $binary,(Join-Path $release 'README.txt'),(Join-Path $release 'Launch-test-babai.cmd'),(Join-Path $release 'Launch-test-battle.cmd'),(Join-Path $release 'Launch-test-dogs.cmd'),(Join-Path $release 'Launch-test-hedgehog.cmd'),(Join-Path $release 'Launch-test-rabid-hedgehog.cmd'),(Join-Path $release 'Launch-test-hedgehogs.cmd'),(Join-Path $release 'Launch-test-mimicrator.cmd'),(Join-Path $release 'Launch-test-toha.cmd'),(Join-Path $release 'Launch-test-illya-toha.cmd'),(Join-Path $release 'SHA256.txt') -DestinationPath (Join-Path $PSScriptRoot ('outputs\Svoi-v'+$version+'-Windows.zip')) -Force
 $previewLauncher='@echo off'+"`r`n"+'start "" "%~dp0Svoi-v'+$version+'.exe" --preview-mimicrator'+"`r`n"
 [IO.File]::WriteAllText((Join-Path $release 'Launch-preview-mimicrator.cmd'),$previewLauncher,[Text.Encoding]::ASCII)
 Compress-Archive -LiteralPath (Join-Path $release 'Launch-preview-mimicrator.cmd') -DestinationPath (Join-Path $PSScriptRoot ('outputs\Svoi-v'+$version+'-Windows.zip')) -Update
 Write-Output $release
} finally { Pop-Location }
