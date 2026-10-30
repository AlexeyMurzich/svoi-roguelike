$ErrorActionPreference='Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
$version=[regex]::Match((Get-Content (Join-Path $PSScriptRoot 'GameVersion.cs') -Raw), 'Number = "([^"]+)"').Groups[1].Value
if(!$version){throw 'Missing game version'}
$compilerPath=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$compilerArgs=@('/nologo','/target:winexe','/optimize+','/warn:4','/warnaserror+','/win32manifest:app.manifest',('/out:Svoi-v'+$version+'.exe'),'/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Core.dll')
$assetRoot=Join-Path $PSScriptRoot 'assets'
$requiredAssets=@('pupsolet.png','illya.png','runtso.png','shuten.png','checkout.png','map-smolevichi.png','map-icons.png','menu-icons.png','hud-texture.png','health-icon.png','coin-icon.png','enemies\giant-rat.png','cards\pupsolet.png','cards\illya.png','cards\runtso.png','cards\shuten.png','enemies\rat-bite.png','enemies\dogs-complete.png','shuten-combat.png','enemies\hedgehog-curl.png','enemies\hedgehog-bite.png')
$requiredAssets+=@('npc\cashier.png','combat-hospital.png','enemies\doctors\male.png','enemies\doctors\female.png')
$requiredAssets+=@('01-idle-hair.png','02-summon.png','03-gorilla.png','04-empress.png','minion-01-glasses.png','minion-03-ponytail.png','minion-04-redhead.png') | ForEach-Object { 'enemies\fomatreshka\'+$_ }
$requiredAssets+=@('combat-social-stage.png','combat-old-center-stage.png','combat-estate-stage.png')
$requiredAssets+=@('enemies\babai-poses.png','enemies\babai-bed.png')
$requiredAssets+=@('enemies\toha-light.png','enemies\toha-dark.png','combat-toha-light.png','combat-toha-dark.png')
$requiredAssets+=@('heroes\pupsolet.png','heroes\illya.png','heroes\runtso.png')
$requiredAssets+=@('combat-icons.png','combat-dock-icons.png','route-button.png','combat-mimicrator.png')
$requiredAssets+=@('enemies\mimicrator-body.png','enemies\mimicrator-props.png','enemies\mimicrator-shell.png')
$requiredAssets+=@('ring','coupon','phone','id','baton','armor','feather','processor','investigation','exchange','mystery','soten-kisshun','zoltraak') | ForEach-Object { 'relics\'+$_+'.png' }
foreach($relativeName in $requiredAssets){
 $imageFile=Get-Item -LiteralPath (Join-Path $assetRoot $relativeName)
 $resourceName=$relativeName.Replace('\','.').Replace('/','.')
 $compilerArgs+=('/resource:assets\'+$relativeName+','+$resourceName)
}
$musicIds=@('pupsolet','illya','runtso','shuten','mimicrator','babai')
foreach($id in $musicIds){
 $musicFile=Get-Item -LiteralPath (Join-Path $assetRoot ('music\'+$id+'.pcm.gz'))
 $compilerArgs+=('/resource:assets\music\'+$id+'.pcm.gz,music.'+$id+'.pcm.gz')
}
foreach($id in @('toha-light','toha-dark')) {
 $soundFile=Get-Item -LiteralPath (Join-Path $assetRoot ('sfx\'+$id+'.pcm.gz'))
 $compilerArgs+=('/resource:assets\sfx\'+$id+'.pcm.gz,sfx.'+$id+'.pcm.gz')
}
$compilerArgs+=('/resource:assets\sfx\cashier\glitch.pcm.gz,sfx.cashier.glitch.pcm.gz')
foreach($hero in @('pupsolet','illya','runtso','shuten')) {
 foreach($kind in @('attack','guard')) {
  $id=$hero+'-'+$kind
  $soundFile=Get-Item -LiteralPath (Join-Path $assetRoot ('sfx\cards\runtime\'+$id+'.pcm.gz'))
  $compilerArgs+=('/resource:assets\sfx\cards\runtime\'+$id+'.pcm.gz,sfx.cards.'+$id+'.pcm.gz')
 }
}
$compilerArgs+=@(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | Sort-Object Name | ForEach-Object { $_.Name })
& $compilerPath @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Build failed'}
} finally { Pop-Location }
