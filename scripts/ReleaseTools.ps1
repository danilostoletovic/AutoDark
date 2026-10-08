Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-ReleaseVersion([string] $Tag) {
    if ($Tag -cnotmatch '^v([1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
        throw 'Use a stable version tag vMAJOR.MINOR.PATCH (major >= 1; no leading zeros, prerelease, or build suffix).'
    }
    $parts = @($Matches[1], $Matches[2], $Matches[3])
    foreach ($part in $parts) {
        $number = 0
        if (![int]::TryParse($part, [ref]$number) -or $number -gt 65534) {
            throw 'Version components must be 0..65534 (the .NET assembly version limit).'
        }
    }
    [pscustomobject]@{ Tag = $Tag; Version = $parts -join '.'; PackageVersion = ($parts -join '.') + '.0' }
}

function Test-PortableExecutable([string] $Path) {
    $file = Get-Item -LiteralPath $Path
    if ($file.Length -le 0) { throw "Empty executable: $Path" }
    $stream = [IO.File]::OpenRead($file.FullName)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        if ($reader.ReadUInt16() -ne 0x5a4d) { throw 'Missing DOS executable header.' }
        $stream.Position = 0x3c
        $pe = $reader.ReadInt32()
        $stream.Position = $pe
        if ($reader.ReadUInt32() -ne 0x4550 -or $reader.ReadUInt16() -ne 0x8664) { throw 'Executable is not Windows AMD64.' }
        $sections = $reader.ReadUInt16()
        $stream.Position = $pe + 20
        $optionalSize = $reader.ReadUInt16()
        $optional = $pe + 24
        $stream.Position = $optional
        if ($reader.ReadUInt16() -ne 0x20b) { throw 'Expected PE32+ executable.' }
        $stream.Position = $optional + 68
        if ($reader.ReadUInt16() -ne 2) { throw 'Expected a Windows GUI executable.' }
        $stream.Position = $optional + 112 + 16
        $resourceRva = $reader.ReadUInt32()
        $resourceOffset = $null
        $nativeLength = 0
        for ($i = 0; $i -lt $sections; $i++) {
            $stream.Position = $optional + $optionalSize + 40 * $i + 8
            $virtualSize = $reader.ReadUInt32(); $rva = $reader.ReadUInt32()
            $rawSize = $reader.ReadUInt32(); $rawOffset = $reader.ReadUInt32()
            $nativeLength = [Math]::Max($nativeLength, [long]$rawOffset + $rawSize)
            if ($resourceRva -ge $rva -and $resourceRva -lt $rva + [Math]::Max($virtualSize, $rawSize)) {
                $resourceOffset = $rawOffset + $resourceRva - $rva
            }
        }
        if ($null -eq $resourceOffset) { throw 'Executable has no resource directory.' }
        $stream.Position = $resourceOffset + 12
        $resourceCount = $reader.ReadUInt16() + $reader.ReadUInt16()
        $hasIcon = $false; $hasGroupIcon = $false
        for ($i = 0; $i -lt $resourceCount; $i++) {
            $type = $reader.ReadUInt32(); $null = $reader.ReadUInt32()
            if ($type -eq 3) { $hasIcon = $true }
            if ($type -eq 14) { $hasGroupIcon = $true }
        }
        if (!$hasIcon -or !$hasGroupIcon) { throw 'Application icon resources are missing.' }

        # .NET's documented apphost bundle marker, followed via its header offset.
        $signature = [byte[]](0x8b,0x12,0x02,0xb9,0x6a,0x61,0x20,0x38,0x72,0x7b,0x93,0x02,0x14,0xd7,0xa0,0x32,0x13,0xf5,0xb9,0xe6,0xef,0xae,0x33,0x18,0xee,0x3b,0x2d,0xce,0x24,0xb3,0x6a,0xae)
        $stream.Position = 0
        $hostBytes = $reader.ReadBytes([int][Math]::Min($file.Length, $nativeLength))
        $encoding = [Text.Encoding]::GetEncoding(28591) # One character per byte; native ordinal search.
        $marker = $encoding.GetString($hostBytes).IndexOf($encoding.GetString($signature), [StringComparison]::Ordinal)
        if ($marker -lt 0) { throw 'Missing .NET single-file bundle marker.' }
        $header = [BitConverter]::ToInt64($hostBytes, $marker - 8)
        if ($header -le 0 -or $header -ge $file.Length) { throw 'Executable is not a populated .NET bundle.' }
        $stream.Position = $header
        if ($reader.ReadUInt32() -ne 6 -or $reader.ReadUInt32() -ne 0) { throw 'Unsupported .NET bundle format; update validation for this SDK.' }
        $count = $reader.ReadInt32(); $null = $reader.ReadString()
        if ($count -lt 1 -or $count -gt 10000) { throw 'Invalid bundle entry count.' }
        $stream.Position += 40 # deps/config offsets and sizes, flags
        $entries = @{}
        for ($i = 0; $i -lt $count; $i++) {
            $offset = $reader.ReadInt64(); $size = $reader.ReadInt64(); $compressed = $reader.ReadInt64()
            $type = $reader.ReadByte(); $name = $reader.ReadString()
            $entries[$name] = [pscustomobject]@{ Offset = $offset; Size = $size; Compressed = $compressed; Type = $type }
        }
        foreach ($required in @('AutoDark.dll','System.Private.CoreLib.dll','System.Windows.Forms.dll','AutoDark.runtimeconfig.json')) {
            if (!$entries.ContainsKey($required)) { throw "Self-contained bundle is missing $required" }
        }
        $config = $entries['AutoDark.runtimeconfig.json']
        if ($config.Compressed -ne 0) { throw 'Unexpected compressed runtime configuration.' }
        $stream.Position = $config.Offset
        $runtime = [Text.Encoding]::UTF8.GetString($reader.ReadBytes([int]$config.Size)) | ConvertFrom-Json
        if ($runtime.runtimeOptions.tfm -ne 'net10.0' -or !$runtime.runtimeOptions.includedFrameworks) {
            throw 'Bundle runtime configuration is not self-contained .NET 10.'
        }
        foreach ($framework in @('Microsoft.NETCore.App','Microsoft.WindowsDesktop.App')) {
            if ($runtime.runtimeOptions.includedFrameworks.name -notcontains $framework) { throw "Missing included framework: $framework" }
        }
        Write-Host "Validated x64 GUI PE, embedded icon, and self-contained .NET 10 bundle: $Path"
    }
    finally { $reader.Dispose(); $stream.Dispose() }
}

function Invoke-ExecutableChecks([string] $Path) {
    $process = Start-Process -FilePath ([IO.Path]::GetFullPath($Path)) -ArgumentList '--self-test' -WindowStyle Hidden -PassThru
    try {
        if (!$process.WaitForExit(60000)) { $process.Kill(); throw 'Executable self-tests exceeded 60 seconds.' }
        if ($process.ExitCode -ne 0) { throw "Executable self-tests failed: exit $($process.ExitCode)" }
    }
    finally { $process.Dispose() }
}

function Test-PortableZip([string] $Zip, [string] $ExpectedExe, [string] $OutputDirectory) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead([IO.Path]::GetFullPath($Zip))
    try {
        if ($archive.Entries.Count -ne 1 -or $archive.Entries[0].FullName -cne 'AutoDark.exe' -or $archive.Entries[0].Length -eq 0) {
            throw 'Portable ZIP must contain exactly one nonempty AutoDark.exe at its root.'
        }
    }
    finally { $archive.Dispose() }
    New-Item -ItemType Directory -Path $OutputDirectory -ErrorAction Stop | Out-Null
    Expand-Archive -LiteralPath $Zip -DestinationPath $OutputDirectory
    $extracted = Join-Path $OutputDirectory 'AutoDark.exe'
    if ((Get-FileHash -LiteralPath $extracted).Hash -ne (Get-FileHash -LiteralPath $ExpectedExe).Hash) { throw 'ZIP executable does not match the published EXE.' }
    Test-PortableExecutable $extracted
    Invoke-ExecutableChecks $extracted
}

function New-PortableZip([string] $Executable, [string] $Zip) {
    Add-Type -AssemblyName System.IO.Compression
    $inputStream = $null
    # Antivirus/image-loader handles can briefly overlap a successful process exit.
    for ($attempt = 0; $attempt -lt 8; $attempt++) {
        try {
            $inputStream = [IO.File]::Open($Executable, [IO.FileMode]::Open, [IO.FileAccess]::Read,
                [IO.FileShare]::ReadWrite -bor [IO.FileShare]::Delete)
            break
        }
        catch [IO.IOException] { if ($attempt -eq 7) { throw }; Start-Sleep -Milliseconds 250 }
    }
    $outputStream = $null; $archive = $null; $entryStream = $null
    try {
        $outputStream = [IO.File]::Open($Zip, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
        $archive = [IO.Compression.ZipArchive]::new($outputStream, [IO.Compression.ZipArchiveMode]::Create)
        $entryStream = $archive.CreateEntry('AutoDark.exe', [IO.Compression.CompressionLevel]::Optimal).Open()
        $inputStream.CopyTo($entryStream)
    }
    finally {
        if ($entryStream) { $entryStream.Dispose() }; if ($archive) { $archive.Dispose() }
        if ($outputStream) { $outputStream.Dispose() }; if ($inputStream) { $inputStream.Dispose() }
    }
}
