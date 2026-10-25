param([Parameter(ValueFromRemainingArguments=$true)][string[]]$GameArguments)
$ErrorActionPreference='Stop'
$version=[regex]::Match((Get-Content (Join-Path $PSScriptRoot 'GameVersion.cs') -Raw), 'Number = "([^"]+)"').Groups[1].Value
& (Join-Path $PSScriptRoot ('Svoi-v'+$version+'.exe')) @GameArguments
