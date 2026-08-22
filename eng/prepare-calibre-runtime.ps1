param(
    [string]$Destination = (Join-Path $PSScriptRoot '..\ThirdParty\calibre\runtime'),
    [string]$Version = '9.13.0',
    [switch]$DownloadSource,
    [string]$SourceDestination
)

$ErrorActionPreference = 'Stop'
$Destination = [IO.Path]::GetFullPath($Destination)
$releaseBase = "https://download.calibre-ebook.com/$Version"
$installerName = "calibre-64bit-$Version.msi"
$sourceName = "calibre-$Version.tar.xz"
$installerUrl = "$releaseBase/$installerName"
$sourceUrl = "$releaseBase/$sourceName"
$work = Join-Path $env:RUNNER_TEMP "PageArc-calibre-$Version"
if ([string]::IsNullOrWhiteSpace($env:RUNNER_TEMP)) {
    $work = Join-Path ([IO.Path]::GetTempPath()) "PageArc-calibre-$Version"
}
Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $work | Out-Null

$msi = Join-Path $work $installerName
Write-Host "Downloading calibre $Version from $installerUrl"
Invoke-WebRequest -Uri $installerUrl -OutFile $msi -UseBasicParsing
if (-not (Test-Path $msi) -or (Get-Item $msi).Length -lt 1MB) { throw 'calibre installer download is missing or unexpectedly small.' }

$extract = Join-Path $work 'extract'
New-Item -ItemType Directory -Force $extract | Out-Null
$process = Start-Process msiexec.exe -ArgumentList @('/a', "`"$msi`"", '/qn', "TARGETDIR=`"$extract`"") -PassThru -Wait
if ($process.ExitCode -ne 0) { throw "calibre administrative extraction failed with exit code $($process.ExitCode)." }

$converter = Get-ChildItem $extract -Recurse -Filter 'ebook-convert.exe' | Select-Object -First 1
if (-not $converter) { throw 'ebook-convert.exe was not found in the extracted calibre installer.' }
$runtimeRoot = Split-Path -Parent $converter.FullName
Remove-Item $Destination -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $Destination | Out-Null
Copy-Item (Join-Path $runtimeRoot '*') $Destination -Recurse -Force
if (-not (Test-Path (Join-Path $Destination 'ebook-convert.exe'))) { throw 'Prepared PageArc calibre runtime is incomplete.' }

if ($DownloadSource) {
    if ([string]::IsNullOrWhiteSpace($SourceDestination)) {
        $SourceDestination = Join-Path (Split-Path -Parent $Destination) $sourceName
    }
    $SourceDestination = [IO.Path]::GetFullPath($SourceDestination)
    New-Item -ItemType Directory -Force (Split-Path -Parent $SourceDestination) | Out-Null
    Write-Host "Downloading corresponding calibre source from $sourceUrl"
    Invoke-WebRequest -Uri $sourceUrl -OutFile $SourceDestination -UseBasicParsing
    if (-not (Test-Path $SourceDestination) -or (Get-Item $SourceDestination).Length -lt 1MB) { throw 'calibre source archive download is missing or unexpectedly small.' }
}

Write-Host "Prepared calibre $Version runtime at $Destination"
Write-Output (Join-Path $Destination 'ebook-convert.exe')
