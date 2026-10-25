[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$IdentityName,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$Publisher,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string]$PublisherDisplayName,
    [ValidateNotNullOrEmpty()][string]$DisplayName = 'PowerPlanTray – Energiepläne automatisch wechseln',
    [ValidateNotNullOrEmpty()][string]$EnglishDisplayName = 'PowerPlanTray – Automatic Power Plan Switching',
    [ValidateNotNullOrEmpty()][string]$PackageVersion = '1.6.13.0',
    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/store'),
    [string]$MakeAppxPath,
    [string]$DotnetPath = 'dotnet'
)
$ErrorActionPreference = 'Stop'
if ($IdentityName -notmatch '^[A-Za-z0-9][A-Za-z0-9.-]{2,49}$' -or $IdentityName -match 'Placeholder') {
    throw 'Supply the exact Package/Identity/Name from Partner Center (3–50 letters, digits, periods or hyphens).'
}
if ($Publisher -notmatch '^CN=.+') { throw 'Publisher must be the exact CN= value from Partner Center.' }
if ($Publisher -match 'Placeholder' -or $PublisherDisplayName -eq 'Placeholder') { throw 'Placeholder identities are not valid Store submissions.' }
if ($PackageVersion -notmatch '^[1-9][0-9]*\.[0-9]+\.[0-9]+\.0$') { throw 'Use a four-part Store version with a nonzero major version and final component 0.' }
if (([version]$PackageVersion).Major -gt 65535 -or ([version]$PackageVersion).Minor -gt 65535 -or ([version]$PackageVersion).Build -gt 65535) {
    throw 'Store version components must be at most 65535.'
}
$projectRoot = Split-Path $PSScriptRoot -Parent
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
if (-not $MakeAppxPath) {
    $sdkBin = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    $MakeAppxPath = Get-ChildItem $sdkBin -Filter makeappx.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object FullName -match '[\\/]x64[\\/]' | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $MakeAppxPath -or -not (Test-Path -LiteralPath $MakeAppxPath)) { throw 'MakeAppx.exe not found. Install Windows SDK or pass -MakeAppxPath.' }

# A unique staging directory prevents older edition files entering a new package.
$stage = Join-Path $outputRoot ('staging-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage | Out-Null
& $DotnetPath publish (Join-Path $projectRoot 'PowerPlanTray.csproj') -c Release -r win-x64 --self-contained true `
    -p:CodexEdition=true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true "-p:PublishDir=$stage/"
if ($LASTEXITCODE -ne 0) { throw 'Store app publish failed.' }
Get-ChildItem -LiteralPath $stage -Filter *.pdb | Remove-Item

[xml]$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'AppxManifest.xml') -Raw
$manifest.Package.Identity.SetAttribute('Name', $IdentityName)
$manifest.Package.Identity.SetAttribute('Publisher', $Publisher)
$manifest.Package.Identity.SetAttribute('Version', $PackageVersion)
$manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
$manifest.Package.Properties.DisplayName = 'ms-resource:AppDisplayName'
$manifest.Package.Properties.Description = 'ms-resource:AppDescription'
$manifest.SelectSingleNode("//*[local-name()='VisualElements']").SetAttribute('DisplayName', 'ms-resource:AppDisplayName')
$manifest.SelectSingleNode("//*[local-name()='VisualElements']").SetAttribute('Description', 'ms-resource:AppDescription')
$manifest.SelectSingleNode("//*[local-name()='StartupTask']").SetAttribute('DisplayName', 'ms-resource:AppDisplayName')
$manifest.Save((Join-Path $stage 'AppxManifest.xml'))

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Strings') -Destination $stage -Recurse
foreach ($language in @('de-DE', 'en-US')) {
    $resourcePath = Join-Path $stage "Strings/$language/Resources.resw"
    [xml]$resources = Get-Content -LiteralPath $resourcePath -Raw
    ($resources.root.data | Where-Object name -eq 'AppDisplayName').value = if ($language -eq 'de-DE') { $DisplayName } else { $EnglishDisplayName }
    $resources.Save($resourcePath)
}
# Reuse PowerPlanTray's own icon. No reference screenshots or third-party artwork.
Add-Type -AssemblyName System.Drawing
$iconBytes = [Convert]::FromBase64String([IO.File]::ReadAllText((Join-Path $projectRoot 'assets/PowerPlanTray.ico.b64')))
$stream = [IO.MemoryStream]::new($iconBytes)
$icon = [Drawing.Icon]::new($stream)
$bitmap = $icon.ToBitmap()
$assetDir = Join-Path $stage 'Assets'
New-Item -ItemType Directory -Path $assetDir | Out-Null
try {
    foreach ($asset in @(@('StoreLogo.png',50), @('Square44x44Logo.png',44), @('Square150x150Logo.png',150))) {
        $size = [int]$asset[1]
        $canvas = [Drawing.Bitmap]::new($size, $size)
        $graphics = [Drawing.Graphics]::FromImage($canvas)
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $inset = [int]($size * 0.1)
            $graphics.DrawImage($bitmap, $inset, $inset, $size - 2 * $inset, $size - 2 * $inset)
            $canvas.Save((Join-Path $assetDir $asset[0]), [Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $graphics.Dispose(); $canvas.Dispose() }
    }
}
finally { $bitmap.Dispose(); $icon.Dispose(); $stream.Dispose() }

$makePri = Join-Path (Split-Path $MakeAppxPath -Parent) 'makepri.exe'
if (-not (Test-Path -LiteralPath $makePri)) { throw 'MakePri.exe required for localized Store names.' }
$priConfig = Join-Path $outputRoot ('priconfig-' + [guid]::NewGuid().ToString('N') + '.xml')
& $makePri createconfig /cf $priConfig /dq en-US
if ($LASTEXITCODE -ne 0) { throw 'Resource configuration failed.' }
# Keep both languages in one self-contained MSIX instead of generating
# detached language resource packages.
[xml]$configuration = Get-Content -LiteralPath $priConfig -Raw
$configuration.resources.packaging.RemoveAll()
$configuration.Save($priConfig)
& $makePri new /pr $stage /cf $priConfig /of (Join-Path $stage 'resources.pri') /mn (Join-Path $stage 'AppxManifest.xml')
if ($LASTEXITCODE -ne 0) { throw 'Localized package resource generation failed.' }


$package = Join-Path $outputRoot "PowerPlanTray-Store-$PackageVersion-x64.msix"
if (Test-Path -LiteralPath $package) { throw "Output already exists: $package. Choose a new version or output directory." }
& $MakeAppxPath pack /d $stage /p $package /h SHA256
if ($LASTEXITCODE -ne 0) { throw 'MSIX validation or packing failed.' }
Get-FileHash -LiteralPath $package -Algorithm SHA256 | Format-List
Write-Output "Unsigned Store submission package: $package"
Write-Output 'Store signing is performed by Microsoft after certification. This unsigned file is not a sideload installer.'
