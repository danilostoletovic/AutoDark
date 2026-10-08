param(
    [Parameter(Mandatory)][string] $Tag,
    [Parameter(Mandatory)][string] $Executable,
    [string] $IdentityName = $env:STORE_IDENTITY_NAME,
    [string] $Publisher = $env:STORE_PUBLISHER,
    [string] $PublisherDisplayName = $env:STORE_PUBLISHER_DISPLAY_NAME,
    [string] $Output = 'artifacts/store',
    [string] $MakeAppxPath
)
. "$PSScriptRoot/ReleaseTools.ps1"
$version = Get-ReleaseVersion $Tag
foreach ($value in @($IdentityName, $Publisher, $PublisherDisplayName)) {
    if ([string]::IsNullOrWhiteSpace($value) -or $value -match '@@|(?i:placeholder|your[_ -]|example)') {
        throw 'Store packaging requires real Identity Name, Publisher, and Publisher Display Name from Partner Center. No placeholders are accepted.'
    }
}
if ($IdentityName -cnotmatch '^[A-Za-z0-9.-]{3,50}$') { throw 'Invalid MSIX Identity Name.' }
if ($Publisher -notmatch '(^|,\s*)CN=') { throw 'Publisher must be the complete CN= distinguished name from Partner Center.' }
$null = [Security.Cryptography.X509Certificates.X500DistinguishedName]::new($Publisher)
Test-PortableExecutable $Executable
$exeVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo([IO.Path]::GetFullPath($Executable)).FileVersion
if ($exeVersion -ne $version.PackageVersion) { throw "Executable version $exeVersion does not match tag $Tag." }
if (!$MakeAppxPath) {
    $sdk = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits/10/bin'
    $tools = @(Get-ChildItem -LiteralPath $sdk -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^10\.[0-9]+\.[0-9]+\.[0-9]+$' } |
        Sort-Object { [version]$_.Name } -Descending |
        ForEach-Object { Join-Path $_.FullName 'x64/makeappx.exe' } |
        Where-Object { Test-Path -LiteralPath $_ })
    if ($tools.Count -eq 0) { throw 'Windows SDK MakeAppx.exe is missing. Install Windows 11 SDK, or pass -MakeAppxPath.' }
    $MakeAppxPath = $tools[0]
}
$toolSignature = Get-AuthenticodeSignature -LiteralPath $MakeAppxPath
if ($toolSignature.Status -ne 'Valid' -or $toolSignature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
    throw 'MakeAppx must have a valid Microsoft Authenticode signature.'
}
$root = Split-Path $PSScriptRoot -Parent
$outputPath = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $outputPath) { throw 'Choose a new, empty Store output directory.' }
$layout = Join-Path $outputPath 'layout'
$assets = Join-Path $layout 'Assets'
New-Item -ItemType Directory -Path $assets -Force | Out-Null
Copy-Item -LiteralPath $Executable -Destination (Join-Path $layout 'AutoDark.exe')
[xml]$manifest = [IO.File]::ReadAllText((Join-Path $root 'packaging/AppxManifest.xml'))
$manifest.Package.Identity.Name = $IdentityName
$manifest.Package.Identity.Publisher = $Publisher
$manifest.Package.Identity.Version = $version.PackageVersion
$manifest.Package.Properties.PublisherDisplayName = $PublisherDisplayName
$manifest.Save((Join-Path $layout 'AppxManifest.xml')) # DOM escapes identity text safely.
Add-Type -AssemblyName System.Drawing
$source = [Drawing.Image]::FromFile((Join-Path $root 'Assets/AutoDark.png'))
try {
    foreach ($asset in @(@('StoreLogo.png',50), @('Square44x44Logo.png',44), @('Square150x150Logo.png',150))) {
        $bitmap = [Drawing.Bitmap]::new([int]$asset[1], [int]$asset[1])
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.DrawImage($source,0,0,[int]$asset[1],[int]$asset[1])
            $bitmap.Save((Join-Path $assets $asset[0]), [Drawing.Imaging.ImageFormat]::Png)
        }
        finally { $graphics.Dispose(); $bitmap.Dispose() }
    }
}
finally { $source.Dispose() }
$package = Join-Path $outputPath "AutoDark-$($version.PackageVersion)-x64.msix"
& $MakeAppxPath pack /d $layout /p $package /o
if ($LASTEXITCODE -ne 0) { throw 'MakeAppx pack/manifest validation failed.' }
if ((Get-Item -LiteralPath $package).Length -le 0) { throw 'Empty MSIX output.' }
$unpacked = Join-Path $outputPath 'validation'
& $MakeAppxPath unpack /p $package /d $unpacked /o
if ($LASTEXITCODE -ne 0) { throw 'MakeAppx could not unpack the generated package.' }
[xml]$actual = [IO.File]::ReadAllText((Join-Path $unpacked 'AppxManifest.xml'))
if ($actual.Package.Identity.Name -cne $IdentityName -or $actual.Package.Identity.Publisher -cne $Publisher -or
    $actual.Package.Identity.Version -ne $version.PackageVersion -or $actual.Package.Identity.ProcessorArchitecture -ne 'x64') {
    throw 'MSIX identity, publisher, architecture, or version mismatch.'
}
if ($actual.Package.Properties.DisplayName -ne 'AutoDark' -or
    $actual.Package.Applications.Application.Executable -ne 'AutoDark.exe' -or
    $actual.OuterXml.Contains('@@')) { throw 'MSIX still contains placeholders or has an incorrect entry point.' }
$namespaces = [Xml.XmlNamespaceManager]::new($actual.NameTable)
$namespaces.AddNamespace('v','http://schemas.microsoft.com/appx/manifest/virtualization/windows10')
$namespaces.AddNamespace('u5','http://schemas.microsoft.com/appx/manifest/uap/windows10/5')
if (!$actual.SelectSingleNode('//u5:ExecutionAlias[@Alias="AutoDark.Store.exe"]',$namespaces) -or
    !$actual.SelectSingleNode('//v:ExcludedKey[text()="HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"]',$namespaces)) {
    throw 'Missing stable activation alias or theme registry virtualization exclusion.'
}
foreach ($required in @('AutoDark.exe','AppxManifest.xml','AppxBlockMap.xml','[Content_Types].xml',
    'Assets/StoreLogo.png','Assets/Square44x44Logo.png','Assets/Square150x150Logo.png')) {
    if (!(Test-Path -LiteralPath (Join-Path $unpacked $required))) { throw "MSIX is missing $required" }
}
foreach ($asset in @(@('StoreLogo.png',50), @('Square44x44Logo.png',44), @('Square150x150Logo.png',150))) {
    $image = [Drawing.Image]::FromFile((Join-Path $unpacked "Assets/$($asset[0])"))
    try { if ($image.Width -ne $asset[1] -or $image.Height -ne $asset[1]) { throw 'MSIX asset dimensions are wrong.' } }
    finally { $image.Dispose() }
}
if ((Get-FileHash -LiteralPath $Executable).Hash -ne (Get-FileHash -LiteralPath (Join-Path $unpacked 'AutoDark.exe')).Hash) {
    throw 'MSIX executable differs from the validated Release executable.'
}
Test-PortableExecutable (Join-Path $unpacked 'AutoDark.exe')
Write-Host "Validated Store submission candidate: $package (unsigned; not for public sideload distribution)."
