param([string]$Wix='', [string]$CacheMotor='')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$taskProject=Join-Path $taskRoot 'src/FG.Precificador/FG.Precificador.csproj'
$taskVersion=([xml](Get-Content -LiteralPath $taskProject -Raw)).Project.PropertyGroup.Version
$taskArtifacts=Join-Path $taskRoot 'artifacts'
$taskPublish=Join-Path $taskArtifacts "FG-Precificador-3D-$taskVersion-portatil"
New-Item -ItemType Directory -Path $taskPublish -Force | Out-Null
& dotnet restore $taskProject --locked-mode
if($LASTEXITCODE){throw 'Falha ao restaurar dependências.'}
& dotnet publish $taskProject -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -o $taskPublish
if($LASTEXITCODE){throw 'Falha ao publicar o aplicativo.'}
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs/GUIA.md') -Destination (Join-Path $taskPublish 'LEIA-ME.md')
Get-ChildItem -LiteralPath (Join-Path $taskRoot 'THIRD-PARTY-NOTICES') -File | Copy-Item -Destination $taskPublish
Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSE') -Destination (Join-Path $taskPublish 'LICENSE-AGPL-3.0.txt')
if(-not $Wix){throw 'Informe -Wix com o WiX 4 para preparar o motor integrado e seu runtime.'}
& (Join-Path $PSScriptRoot 'Preparar-Motor.ps1') -Destino (Join-Path $taskPublish 'Engine') -Wix $Wix -Cache $CacheMotor
$taskLock=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'engine.lock.json') -Raw | ConvertFrom-Json
$taskSourceCache=if($CacheMotor){$CacheMotor}else{Join-Path $taskRoot 'artifacts/engine-cache'}
Copy-Item -LiteralPath (Join-Path $taskSourceCache $taskLock.sourceDownloadName) -Destination (Join-Path $taskArtifacts $taskLock.sourceDownloadName) -Force
$taskArchive=Join-Path $taskArtifacts "FG-Precificador-3D-$taskVersion-portatil.zip"
Compress-Archive -LiteralPath $taskPublish -DestinationPath $taskArchive -Force
if($Wix){
    $taskInstaller=Join-Path $taskRoot 'src/FG.Precificador/Gerar-Instalador.ps1'
    & $taskInstaller -Publicacao $taskPublish -Saida (Join-Path $taskArtifacts "FG-Precificador-3D-$taskVersion-x64.msi") -Wix $Wix -Versao $taskVersion
}
