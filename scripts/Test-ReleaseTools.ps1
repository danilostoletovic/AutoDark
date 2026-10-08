. "$PSScriptRoot/ReleaseTools.ps1"
$checks = 0
foreach ($tag in @('v1.0.0','v1.0.1','v1.1.0','v65534.65534.65534')) {
    $result = Get-ReleaseVersion $tag
    if ($result.PackageVersion -ne ($tag.Substring(1) + '.0')) { throw "Version mapping failed: $tag" }
    $checks++
}
foreach ($tag in @('1.0.0','v0.1.0','v01.0.0','v1.01.0','v1.0.0.1','v1.0.0-beta','v1.0.0+build','v65535.0.0','v99999999999999.0.0','v1.0.0/anything')) {
    $rejected = $false
    try { $null = Get-ReleaseVersion $tag } catch { $rejected = $true }
    if (!$rejected) { throw "Invalid tag accepted: $tag" }
    $checks++
}
Write-Host "$checks release-version checks passed."

$root = Split-Path $PSScriptRoot -Parent
[xml]$manifest = [IO.File]::ReadAllText((Join-Path $root 'packaging/AppxManifest.xml'))
$namespaces = [Xml.XmlNamespaceManager]::new($manifest.NameTable)
$namespaces.AddNamespace('v','http://schemas.microsoft.com/appx/manifest/virtualization/windows10')
$namespaces.AddNamespace('u5','http://schemas.microsoft.com/appx/manifest/uap/windows10/5')
if ($manifest.Package.Identity.Name -cne '@@STORE_IDENTITY_NAME@@' -or $manifest.Package.Identity.Publisher -cne '@@STORE_PUBLISHER@@') {
    throw 'Store template must not contain an invented/committed identity.'
}
if (!$manifest.SelectSingleNode('//u5:ExecutionAlias[@Alias="AutoDark.Store.exe"]',$namespaces) -or
    !$manifest.SelectSingleNode('//v:ExcludedKey[text()="HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"]',$namespaces)) {
    throw 'Manifest is missing packaged scheduling or real-theme registry access.'
}
Write-Host '2 Store template checks passed.'

# Exercise confidentiality, round-trip recovery, and rejection of tampered/wrong-key artifacts.
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('AutoDark-crypto-tests-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
try {
    $key = [byte[]]::new(32)
    $random = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $random.GetBytes($key) } finally { $random.Dispose() }
    $keyText = [Convert]::ToBase64String($key)
    $inputFile = Join-Path $scratch 'input.bin'
    $encrypted = Join-Path $scratch 'input.enc'
    $decrypted = Join-Path $scratch 'output.bin'
    [IO.File]::WriteAllText($inputFile,'Test fixture: not a real MSIX package.')
    & "$PSScriptRoot/Protect-StoreArtifact.ps1" -Mode Encrypt -InputFile $inputFile -OutputFile $encrypted -KeyBase64 $keyText
    & "$PSScriptRoot/Protect-StoreArtifact.ps1" -Mode Decrypt -InputFile $encrypted -OutputFile $decrypted -KeyBase64 $keyText
    if ((Get-FileHash $inputFile).Hash -ne (Get-FileHash $decrypted).Hash) { throw 'Encrypted artifact round-trip failed.' }
    $tampered = [IO.File]::ReadAllBytes($encrypted); $tampered[30] = $tampered[30] -bxor 1
    $tamperedFile = Join-Path $scratch 'tampered.enc'; [IO.File]::WriteAllBytes($tamperedFile,$tampered)
    $rejected = $false
    try { & "$PSScriptRoot/Protect-StoreArtifact.ps1" -Mode Decrypt -InputFile $tamperedFile -OutputFile (Join-Path $scratch 'bad.bin') -KeyBase64 $keyText } catch { $rejected = $true }
    if (!$rejected) { throw 'Modified artifact accepted.' }
    $key[0] = $key[0] -bxor 1; $rejected = $false
    try { & "$PSScriptRoot/Protect-StoreArtifact.ps1" -Mode Decrypt -InputFile $encrypted -OutputFile (Join-Path $scratch 'wrong.bin') -KeyBase64 ([Convert]::ToBase64String($key)) } catch { $rejected = $true }
    if (!$rejected) { throw 'Wrong artifact key accepted.' }
    Write-Host '3 artifact encryption/authentication checks passed.'
}
finally {
    # Only the exact, freshly created test directory is removed.
    $resolved = [IO.Path]::GetFullPath($scratch)
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if (!$resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe test cleanup path.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
