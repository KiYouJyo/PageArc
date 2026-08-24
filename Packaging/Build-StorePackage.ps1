[CmdletBinding()]
param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '..\artifacts\store-package'),
    [ValidateSet('x64','ARM64')][string]$Platform = 'x64',
    [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
    [switch]$ValidationOnly
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$manifestPath = Join-Path $repo 'Package.Store.appxmanifest'
$expectedName = 'JoKiy.PageArc'
$expectedPublisher = 'CN=C4E4B33A-7B77-4121-897C-7D720A5471F8'
$expectedDisplayName = 'Jo Kiyō'
$expectedFamilyName = 'JoKiy.PageArc_4wdwgytaw3v2m'

if (-not (Test-Path $manifestPath)) { throw "Store manifest not found: $manifestPath" }
[xml]$manifest = Get-Content -Raw -LiteralPath $manifestPath
$identity = $manifest.Package.Identity
if ($identity.Name -ne $expectedName) { throw "Store Identity.Name mismatch: $($identity.Name)" }
if ($identity.Publisher -ne $expectedPublisher) { throw "Store Identity.Publisher mismatch: $($identity.Publisher)" }
$display = $manifest.Package.Properties.PublisherDisplayName
if ($display -ne $expectedDisplayName) { throw "Store PublisherDisplayName mismatch: $display" }

$out = [IO.Path]::GetFullPath((Join-Path $repo $OutputDirectory))
if (-not $ValidationOnly -and (Test-Path $out)) {
    Remove-Item -LiteralPath $out -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $out | Out-Null
$args = @(
    (Join-Path $repo 'PageArc.csproj'), '-t:Rebuild',
    ('-p:Configuration=' + $Configuration), ('-p:Platform=' + $Platform),
    '-p:PageArcDistributionChannel=Store', '-p:WindowsPackageType=MSIX',
    '-p:GenerateAppxPackageOnBuild=true', '-p:AppxPackageSigningEnabled=false',
    '-p:AppxBundle=Always', ('-p:AppxBundlePlatforms=' + $Platform),
    '-p:UapAppxPackageBuildMode=StoreUpload', '-p:AppxSymbolPackageEnabled=false',
    ('-p:AppxPackageDir=' + ($out.TrimEnd('\') + '\'))
)
if (-not $ValidationOnly) {
    & dotnet msbuild @args
    if ($LASTEXITCODE -ne 0) { throw "Store candidate build failed with exit code $LASTEXITCODE" }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$version = $identity.Version
$runtimeIdentifier = 'win-' + $Platform.ToLowerInvariant()
$buildRoot = Join-Path $repo "bin\$Platform\$Configuration\net10.0-windows10.0.26100.0\$runtimeIdentifier\Upload"
$mainPackage = Join-Path $buildRoot "PageArc_${version}\PageArc_${version}_$Platform.msix"
if (-not (Test-Path $mainPackage)) { throw "StoreUpload main package not found: $mainPackage" }
# The SDK also emits a same-named package outside Upload. Never bundle that
# package: it can contain a stale/minimal PRI and only EN-US in its manifest.
# The SDK's scale packages also repeat Assets\StoreLogo.png. The StoreUpload
# main package already contains the complete resource set (including all
# declared languages), so adding those scale packages makes MakeAppx reject
# the bundle for duplicate resources.
$stage = Join-Path ([IO.Path]::GetTempPath()) ('pagearc-store-stage-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stage | Out-Null
Copy-Item -LiteralPath $mainPackage -Destination (Join-Path $stage (Split-Path $mainPackage -Leaf))
$bundlePath = Join-Path $out "PageArc_${version}_x64_bundle.msixbundle"
& 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe' bundle /d $stage /p $bundlePath /o
if ($LASTEXITCODE -ne 0) { throw 'Store bundle creation failed.' }
$uploadPath = Join-Path $out "PageArc_${version}_x64_bundle.msixupload"
$uploadPath | ForEach-Object { if (Test-Path $_) { Remove-Item -LiteralPath $_ -Force } }
$uploadStage = Join-Path ([IO.Path]::GetTempPath()) ('pagearc-store-upload-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $uploadStage | Out-Null
Copy-Item -LiteralPath $bundlePath -Destination $uploadStage
[IO.Compression.ZipFile]::CreateFromDirectory($uploadStage, $uploadPath)
$bundle = Get-Item $bundlePath
$packages = @(Get-ChildItem -LiteralPath $stage -Filter 'PageArc_*.msix' -File)

$results = foreach ($package in $packages) {
    $zip = [IO.Compression.ZipFile]::OpenRead($package.FullName)
    try {
        $entry = $zip.GetEntry('AppxManifest.xml')
        if ($null -eq $entry) { throw "AppxManifest.xml missing from $($package.Name)" }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml]$packageManifest = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $packageIdentity = $packageManifest.Package.Identity
        $languages = @($packageManifest.Package.Resources.Resource | Where-Object { $_.Language } | ForEach-Object { $_.Language.ToUpperInvariant() })
        $isMainPackage = $package.Name -eq (Split-Path $mainPackage -Leaf)
        [pscustomobject]@{
            File = $package.FullName
            Name = $packageIdentity.Name
            Publisher = $packageIdentity.Publisher
            Version = $packageIdentity.Version
            NameValid = ($packageIdentity.Name -eq $expectedName)
            PublisherValid = ($packageIdentity.Publisher -eq $expectedPublisher)
            Languages = ($languages -join ',')
            LanguagesValid = (-not $isMainPackage) -or ((@('EN-US','JA-JP','ZH-CN') | Where-Object { $languages -notcontains $_ }).Count -eq 0)
        }
    } finally { $zip.Dispose() }
}
if ($results.NameValid -contains $false -or $results.PublisherValid -contains $false -or $results.LanguagesValid -contains $false) {
    $results | Format-Table | Out-String | Write-Error
    throw 'Store package identity or language validation failed.'
}
$metadata = [ordered]@{
    channel = 'Microsoft Store'
    packageFamilyName = $expectedFamilyName
    identityName = $expectedName
    publisher = $expectedPublisher
    publisherDisplayName = $expectedDisplayName
    storeUploadPackage = if (Test-Path $uploadPath) { $uploadPath } else { $null }
    packages = @($results)
    storeUpload = 'Generated by UapAppxPackageBuildMode=StoreUpload with symbol package generation disabled.'
}
$metadata | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 (Join-Path $out 'store-package-validation.json')
$metadata | ConvertTo-Json -Depth 5
Remove-Item -LiteralPath $stage, $uploadStage -Recurse -Force -ErrorAction SilentlyContinue
