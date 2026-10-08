param(
    [Parameter(Mandatory)][string] $Tag,
    [Parameter(Mandatory)][string] $Executable,
    [string] $Output = 'artifacts/installer',
    [string] $CompilerPath
)
. "$PSScriptRoot/ReleaseTools.ps1"
$version = Get-ReleaseVersion $Tag
$exe = [IO.Path]::GetFullPath($Executable)
Test-PortableExecutable $exe
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion -ne $version.PackageVersion) {
    throw 'Installer payload version does not match the release tag.'
}
if (!$CompilerPath) {
    $CompilerPath = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
}
if (!(Test-Path -LiteralPath $CompilerPath)) { throw 'Install Inno Setup 6 or supply -CompilerPath pointing to ISCC.exe.' }
$outputPath = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $outputPath) { throw 'Choose a new, empty installer output directory.' }
$script = Join-Path (Split-Path $PSScriptRoot -Parent) 'packaging/AutoDark.iss'
& $CompilerPath "/DAppVersion=$($version.Version)" "/DSourceExe=$exe" "/DOutputDirectory=$outputPath" $script
if ($LASTEXITCODE -ne 0) { throw 'Inno Setup compilation failed.' }
$installer = Join-Path $outputPath 'AutoDark-Setup-win-x64.exe'
if (!(Test-Path -LiteralPath $installer) -or (Get-Item -LiteralPath $installer).Length -le 0) {
    throw 'Installer output is missing or empty.'
}
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($installer).ProductVersion.Trim() -ne $version.Version) {
    throw 'Installer version does not match the release tag.'
}
Write-Host "Validated installer output: $installer"
