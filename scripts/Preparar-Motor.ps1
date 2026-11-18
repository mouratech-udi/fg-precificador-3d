param(
    [Parameter(Mandatory)][string]$Destino,
    [Parameter(Mandatory)][string]$Wix,
    [string]$Cache=''
)
$ErrorActionPreference='Stop'
$taskRepo=Split-Path -Parent $PSScriptRoot
if(-not $Cache){$Cache=Join-Path $taskRepo 'artifacts/engine-cache'}
$taskCache=[IO.Path]::GetFullPath($Cache)
$taskDest=[IO.Path]::GetFullPath($Destino)
New-Item -ItemType Directory -Path $taskCache,$taskDest -Force | Out-Null
$taskLock=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'engine.lock.json') -Raw | ConvertFrom-Json
function DownloadChecked([string]$url,[string]$file,[string]$expected){
    if(-not (Test-Path -LiteralPath $file)){Invoke-WebRequest -Uri $url -OutFile $file}
    if((Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -ne $expected){throw "Integridade do pacote não confere: $([IO.Path]::GetFileName($file))."}
}
$taskBinary=Join-Path $taskCache 'bambu-windows.zip'
$taskSource=Join-Path $taskCache $taskLock.sourceDownloadName
$taskRuntime=Join-Path $taskCache 'vc_redist.x64.exe'
DownloadChecked $taskLock.binaryUrl $taskBinary $taskLock.binarySha256
DownloadChecked $taskLock.sourceUrl $taskSource $taskLock.sourceSha256
DownloadChecked $taskLock.runtimeUrl $taskRuntime $taskLock.runtimeSha256
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskZip=[IO.Compression.ZipFile]::OpenRead($taskBinary)
try{
    foreach($taskEntry in $taskZip.Entries){
        $taskName=$taskEntry.FullName
        if($taskName.EndsWith('/')){continue}
        $taskSelected=($taskName -notmatch '/' -and [IO.Path]::GetExtension($taskName) -in @('.dll','.exe','.txt','.md')) -or $taskName.StartsWith('resources/profiles/')
        if(-not $taskSelected){continue}
        $taskTarget=[IO.Path]::GetFullPath((Join-Path $taskDest $taskName))
        if(-not $taskTarget.StartsWith($taskDest+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Caminho inválido no pacote do motor.'}
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($taskTarget)) -Force | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($taskEntry,$taskTarget,$true)
    }
}finally{$taskZip.Dispose()}
# The official ZIP has no networking plugin. Only root binaries and printer profiles
# are selected: no account data, web application, updater or printing/network module.
$taskLicenseDir=Join-Path $taskDest 'Licenses'
New-Item -ItemType Directory -Path $taskLicenseDir -Force | Out-Null
$taskSourceZip=[IO.Compression.ZipFile]::OpenRead($taskSource)
try{
    foreach($taskEntry in $taskSourceZip.Entries){
        if($taskEntry.Length -eq 0 -or $taskEntry.Length -gt 250000){continue}
        $taskRelative=($taskEntry.FullName -split '/',2)[1]
        if(-not $taskRelative){continue}
        $taskFileName=[IO.Path]::GetFileName($taskRelative)
        if($taskFileName -notmatch '^(LICENSE|LICENCE|COPYING|NOTICE|PATENTS)([._-]|$)'){continue}
        $taskTarget=[IO.Path]::GetFullPath((Join-Path $taskLicenseDir ('Upstream/'+$taskRelative)))
        if(-not $taskTarget.StartsWith($taskLicenseDir+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Caminho inválido nas licenças.'}
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($taskTarget)) -Force | Out-Null
        [IO.Compression.ZipFileExtensions]::ExtractToFile($taskEntry,$taskTarget,$true)
    }
}finally{$taskSourceZip.Dispose()}
$taskRuntimeDir=Join-Path $taskCache 'vcrt'
$taskRuntimeBa=Join-Path $taskCache 'vcrt-license'
if(-not (Test-Path -LiteralPath (Join-Path $taskRuntimeBa 'license.rtf'))){
    & $Wix burn extract $taskRuntime -o $taskRuntimeDir -oba $taskRuntimeBa
    if($LASTEXITCODE){throw 'Falha ao extrair bibliotecas de execução do motor.'}
}
$taskWanted=@('msvcp140.dll','msvcp140_1.dll','msvcp140_2.dll','msvcp140_atomic_wait.dll','msvcp140_codecvt_ids.dll','vcruntime140.dll','vcruntime140_1.dll','concrt140.dll')
$taskInstaller=New-Object -ComObject WindowsInstaller.Installer
foreach($taskMsi in (Get-ChildItem -LiteralPath $taskRuntimeDir -Recurse -File -Filter '*.msi' | Where-Object {$_.Name -in @('vc_runtimeMinimum_x64.msi','vc_runtimeAdditional_x64.msi')})){
    $taskExpanded=Join-Path $taskMsi.DirectoryName 'expanded'
    New-Item -ItemType Directory -Path $taskExpanded -Force | Out-Null
    & "$env:SystemRoot\System32\expand.exe" '-F:*' (Join-Path $taskMsi.DirectoryName 'cab1.cab') $taskExpanded | Out-Null
    if($LASTEXITCODE){throw 'Falha ao extrair DLLs do runtime.'}
    $taskDatabase=$taskInstaller.OpenDatabase($taskMsi.FullName,0)
    $taskView=$taskDatabase.OpenView('SELECT `File`, `FileName` FROM `File`');$taskView.Execute()
    while($taskRecord=$taskView.Fetch()){
        $taskFileName=($taskRecord.StringData(2) -split '\|')[-1]
        if($taskFileName -in $taskWanted){Copy-Item -LiteralPath (Join-Path $taskExpanded $taskRecord.StringData(1)) -Destination (Join-Path $taskDest $taskFileName)}
    }
    $taskView.Close()
}
foreach($taskRequired in @('bambu-studio.exe','BambuStudio.dll','msvcp140.dll','msvcp140_codecvt_ids.dll','vcruntime140.dll','vcruntime140_1.dll','concrt140.dll')){
    if(-not (Test-Path -LiteralPath (Join-Path $taskDest $taskRequired))){throw "Biblioteca ausente no motor: $taskRequired"}
}
Copy-Item -LiteralPath (Join-Path $taskRuntimeBa 'license.rtf') -Destination (Join-Path $taskLicenseDir 'Microsoft-Visual-C-Runtime.rtf')
Copy-Item -LiteralPath (Join-Path $taskRepo 'THIRD-PARTY-NOTICES/BambuStudio-AGPL-3.0.txt') -Destination (Join-Path $taskLicenseDir 'BambuStudio-AGPL-3.0.txt')
Copy-Item -LiteralPath (Join-Path $taskRepo 'docs/MOTOR.md') -Destination (Join-Path $taskDest 'MOTOR-E-FONTES.md')
$taskHashes=[ordered]@{}
Get-ChildItem -LiteralPath $taskDest -File | Where-Object {$_.Extension -in @('.dll','.exe')} | Sort-Object Name | ForEach-Object {$taskHashes[$_.Name]=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
$taskManifest=[ordered]@{name='BambuStudio';version=$taskLock.version;binaryUrl=$taskLock.binaryUrl;binarySha256=$taskLock.binarySha256;sourceUrl=$taskLock.sourceUrl;sourceSha256=$taskLock.sourceSha256;networkPluginIncluded=$false;files=$taskHashes}
$taskManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskDest 'engine-manifest.json') -Encoding utf8
Write-Output "Motor integrado $($taskLock.version) preparado em $taskDest"
