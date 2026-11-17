param([string]$Wix='')
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
$taskArchive=Join-Path $taskArtifacts "FG-Precificador-3D-$taskVersion-portatil.zip"
Compress-Archive -LiteralPath $taskPublish -DestinationPath $taskArchive -Force
if($Wix){
    $taskInstaller=Join-Path $taskRoot 'src/FG.Precificador/Gerar-Instalador.ps1'
    & $taskInstaller -Publicacao $taskPublish -Saida (Join-Path $taskArtifacts "FG-Precificador-3D-$taskVersion-x64.msi") -Wix $Wix -Versao $taskVersion
}
