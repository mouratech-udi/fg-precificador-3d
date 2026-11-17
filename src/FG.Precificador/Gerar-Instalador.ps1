param(
    [Parameter(Mandatory)][string]$Publicacao,
    [Parameter(Mandatory)][string]$Saida,
    [string]$Wix='wix',
    [string]$Versao='1.2.0'
)
$ErrorActionPreference='Stop'
$taskPublish=(Resolve-Path -LiteralPath $Publicacao).Path
$taskDest=[IO.Path]::GetFullPath($Saida)
$taskIntermediate=Join-Path $PSScriptRoot 'instalador-build'
New-Item -ItemType Directory -Path $taskIntermediate -Force | Out-Null
function StableId([string]$key){
    $taskBytes=[Text.Encoding]::UTF8.GetBytes('FG.DesignPrint.Precificador/'+$key.ToLowerInvariant())
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskBytes)).Substring(0,32)
}
function EscapeXml([string]$value){[Security.SecurityElement]::Escape($value)}
$taskParts=[Collections.Generic.List[string]]::new()
foreach($taskFile in (Get-ChildItem -LiteralPath $taskPublish -File)){
    $taskId=StableId $taskFile.Name
    $taskGuid=[Guid]::ParseExact($taskId,'N')
    $taskFileId=if($taskFile.Name -eq 'FG.Precificador.exe'){'MainExe'}else{"F$taskId"}
    $taskParts.Add(('<Component Id="C{0}" Guid="{1}"><File Id="{2}" Source="{3}" KeyPath="yes" /></Component>' -f $taskId,$taskGuid,$taskFileId,(EscapeXml $taskFile.FullName)))
}
$taskRefs=foreach($taskLine in $taskParts){if($taskLine -match '^<Component Id="([^"]+)"'){ '<ComponentRef Id="'+$Matches[1]+'"/>' }}
$taskXml=@"
<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">
<Package Name="FG Precificador 3D" Manufacturer="FG Design Print" Version="$Versao" Language="1046" UpgradeCode="B72C3E94-861A-4DF5-ADF5-24DF6528719C" Scope="perUser" InstallerVersion="500">
<MajorUpgrade DowngradeErrorMessage="Uma versão mais recente já está instalada." Schedule="afterInstallInitialize"/>
<MediaTemplate EmbedCab="yes" CompressionLevel="medium"/>
<Icon Id="AppIcon" SourceFile="$(EscapeXml (Join-Path $PSScriptRoot 'FG.ico'))"/>
<Property Id="ARPPRODUCTICON" Value="AppIcon"/>
<StandardDirectory Id="LocalAppDataFolder"><Directory Id="ProgramsDir" Name="Programs"><Directory Id="INSTALLFOLDER" Name="FG Precificador 3D">
$($taskParts -join [Environment]::NewLine)
<Component Id="Shortcut" Guid="420D715E-6C41-4443-8420-7EC73E5D7507">
<Shortcut Id="StartShortcut" Directory="ProgramMenuFolder" Name="FG Precificador 3D" Target="[#MainExe]" WorkingDirectory="INSTALLFOLDER" Icon="AppIcon"/>
<Shortcut Id="DesktopShortcut" Directory="DesktopFolder" Name="FG Precificador 3D" Target="[#MainExe]" WorkingDirectory="INSTALLFOLDER" Icon="AppIcon"/>
<RegistryValue Root="HKCU" Key="Software\FG Design Print\Precificador" Name="InstallPath" Type="string" Value="[INSTALLFOLDER]" KeyPath="yes"/>
</Component></Directory></Directory></StandardDirectory>
<StandardDirectory Id="ProgramMenuFolder"/>
<StandardDirectory Id="DesktopFolder"/>
<Feature Id="Application" Title="FG Precificador 3D" Level="1"><ComponentRef Id="Shortcut"/>
$($taskRefs -join [Environment]::NewLine)
</Feature></Package></Wix>
"@
$taskSource=Join-Path $taskIntermediate 'fg-precificador.wxs'
Set-Content -LiteralPath $taskSource -Value $taskXml -Encoding utf8
& $Wix build -arch x64 -intermediateFolder $taskIntermediate -pdbtype none -o $taskDest $taskSource
if($LASTEXITCODE){throw "Falha ao criar o instalador ($LASTEXITCODE)."}
