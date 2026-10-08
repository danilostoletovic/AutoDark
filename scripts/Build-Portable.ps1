param([Parameter(Mandatory)][string] $Tag, [string] $Output = 'artifacts/release', [switch] $SkipAudit)
. "$PSScriptRoot/ReleaseTools.ps1"
$version = Get-ReleaseVersion $Tag
$root = Split-Path $PSScriptRoot -Parent
$outputPath = [IO.Path]::GetFullPath($Output)
if (Test-Path -LiteralPath $outputPath) { throw 'Choose a new, empty release output directory.' }
New-Item -ItemType Directory -Path $outputPath | Out-Null
$stage = Join-Path $outputPath 'publish'
$arguments = @('publish', (Join-Path $root 'AutoDark.csproj'), '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true',
    '-p:PublishProfile=Portable', "-p:Version=$($version.Version)", "-p:FileVersion=$($version.PackageVersion)", '-o', $stage, '-warnaserror')
if ($SkipAudit) { $arguments += '-p:NuGetAudit=false' }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Portable dotnet publish failed.' }
$files = @(Get-ChildItem -LiteralPath $stage -File -Recurse)
if ($files.Count -ne 1 -or $files[0].Name -cne 'AutoDark.exe') { throw 'Single-file publish unexpectedly produced additional files.' }
$exe = Join-Path $outputPath 'AutoDark.exe'
Copy-Item -LiteralPath $files[0].FullName -Destination $exe
if ([Diagnostics.FileVersionInfo]::GetVersionInfo($exe).FileVersion -ne $version.PackageVersion) {
    throw 'Published executable version does not match the release tag.'
}
Test-PortableExecutable $exe
Invoke-ExecutableChecks $exe
$zip = Join-Path $outputPath 'AutoDark-win-x64.zip'
New-PortableZip $exe $zip
Test-PortableZip $zip $exe (Join-Path $outputPath 'zip-check')
Write-Host "Release artifacts: $exe and $zip"
