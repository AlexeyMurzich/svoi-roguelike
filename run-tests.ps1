param([string]$ResultDirectory=(Join-Path $PSScriptRoot 'test-results'), [string]$Filter='')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
& (Join-Path $PSScriptRoot 'Tests/build-tests.ps1')
& (Join-Path $PSScriptRoot 'Svoi.Tests.exe') $ResultDirectory $Filter
if($LASTEXITCODE -ne 0){throw 'Regression tests failed; see test-results.txt'}
