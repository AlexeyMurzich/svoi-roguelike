$ErrorActionPreference='Stop'
Push-Location -LiteralPath $PSScriptRoot
try {
$compilerPath=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$compilerArgs=@('/nologo','/target:winexe','/optimize+','/warn:4','/warnaserror+','/win32manifest:app.manifest','/out:Svoi-v0.11.exe','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Core.dll')
$assetRoot=Join-Path $PSScriptRoot 'assets'
$requiredAssets=@('pupsolet.png','illya.png','runtso.png','shuten.png','checkout.png','map-smolevichi.png','map-icons.png','menu-icons.png','hud-texture.png','health-icon.png','coin-icon.png','enemies\giant-rat.png','cards\pupsolet.png','cards\illya.png','cards\runtso.png','cards\shuten.png','enemies\rat-bite.png','enemies\stray-dogs.png','shuten-attack.png','shuten-defense.png','enemies\dog-body.png')
$requiredAssets+=@('ring','coupon','phone','id','baton','armor','feather','processor','investigation','exchange','mystery') | ForEach-Object { 'relics\'+$_+'.png' }
foreach($relativeName in $requiredAssets){
 $imageFile=Get-Item -LiteralPath (Join-Path $assetRoot $relativeName)
 $resourceName=$relativeName.Replace('\','.').Replace('/','.')
 $compilerArgs+=('/resource:'+$imageFile.FullName+','+$resourceName)
}
$compilerArgs+=@('Program.cs','Game.cs','GameModel.cs','CheckoutArt.cs','StartingBuffs.cs','ImageAssets.cs','Viewport.cs','ActMap.cs','MapScreen.cs','MapLayout.cs','MapIcons.cs','RunMenu.cs','ResponsiveMap.cs','Fullscreen.cs','HudArt.cs','CombatState.cs','CombatScreen.cs','CombatMotion.cs','MapMotion.cs','Motion.cs','SceneBackground.cs','Enemies.cs','HowlSound.cs','DogMotion.cs','AttackTimeline.cs','MotionDriver.cs','CombatLayout.cs','InvestigationTooltip.cs','CombatEnemy.cs','DeathVisuals.cs','SpriteDrawing.cs')
& $compilerPath @compilerArgs
if($LASTEXITCODE -ne 0){throw 'Build failed'}
} finally { Pop-Location }
