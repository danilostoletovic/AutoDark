param(
    [Parameter(Mandatory)][ValidateSet('Encrypt','Decrypt')][string] $Mode,
    [Parameter(Mandatory)][string] $InputFile,
    [Parameter(Mandatory)][string] $OutputFile,
    [string] $KeyBase64 = $env:STORE_ARTIFACT_KEY_BASE64
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($KeyBase64)) { throw 'Set STORE_ARTIFACT_KEY_BASE64 to a securely generated 32-byte base64 key. Never commit it.' }
$key = [Convert]::FromBase64String($KeyBase64)
if ($key.Length -ne 32) { throw 'Store artifact key must decode to exactly 32 bytes.' }
if (Test-Path -LiteralPath $OutputFile) { throw 'Refusing to overwrite an existing output file.' }
$magic = [Text.Encoding]::ASCII.GetBytes('ADMSIX01')
$derive = [Security.Cryptography.HMACSHA256]::new($key)
$aes = [Security.Cryptography.Aes]::Create()
$mac = $null
try {
    $macKey = $derive.ComputeHash([Text.Encoding]::ASCII.GetBytes('AutoDark Store artifact MAC v1'))
    $mac = [Security.Cryptography.HMACSHA256]::new($macKey)
    $aes.Key = $key
    $aes.Mode = [Security.Cryptography.CipherMode]::CBC
    $aes.Padding = [Security.Cryptography.PaddingMode]::PKCS7
    $inputBytes = [IO.File]::ReadAllBytes([IO.Path]::GetFullPath($InputFile))
    if ($Mode -eq 'Encrypt') {
        $aes.GenerateIV()
        $transform = $aes.CreateEncryptor()
        try { $cipher = $transform.TransformFinalBlock($inputBytes, 0, $inputBytes.Length) }
        finally { $transform.Dispose() }
        $authenticated = [byte[]]::new(24 + $cipher.Length)
        $magic.CopyTo($authenticated,0); $aes.IV.CopyTo($authenticated,8); $cipher.CopyTo($authenticated,24)
        $tag = $mac.ComputeHash($authenticated)
        $outputBytes = [byte[]]::new($authenticated.Length + 32)
        $authenticated.CopyTo($outputBytes,0); $tag.CopyTo($outputBytes,$authenticated.Length)
    }
    else {
        if ($inputBytes.Length -lt 72 -or ($inputBytes.Length - 56) % 16 -ne 0) { throw 'Invalid encrypted Store artifact length.' }
        for ($i = 0; $i -lt 8; $i++) { if ($inputBytes[$i] -ne $magic[$i]) { throw 'Unsupported Store artifact format.' } }
        $expected = $mac.ComputeHash($inputBytes,0,$inputBytes.Length-32)
        $difference = 0
        for ($i = 0; $i -lt 32; $i++) { $difference = $difference -bor ($expected[$i] -bxor $inputBytes[$inputBytes.Length-32+$i]) }
        if ($difference -ne 0) { throw 'Store artifact authentication failed: wrong key or modified file.' }
        $aes.IV = [byte[]]$inputBytes[8..23]
        $transform = $aes.CreateDecryptor()
        try { $outputBytes = $transform.TransformFinalBlock($inputBytes,24,$inputBytes.Length-56) }
        finally { $transform.Dispose() }
    }
    [IO.File]::WriteAllBytes([IO.Path]::GetFullPath($OutputFile),$outputBytes)
    Write-Host "Store artifact $Mode completed."
}
finally {
    $derive.Dispose(); $aes.Dispose(); if ($mac) { $mac.Dispose() }
    [Array]::Clear($key,0,$key.Length)
}
