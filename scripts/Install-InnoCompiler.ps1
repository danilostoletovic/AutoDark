param([string] $Output = 'artifacts/inno-tools')
$ErrorActionPreference = 'Stop'
$outputPath = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$download = Join-Path $outputPath 'innosetup-6.7.3.exe'
if (!(Test-Path -LiteralPath $download)) {
    Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe' -OutFile $download -UseBasicParsing
}
if ((Get-FileHash -LiteralPath $download -Algorithm SHA256).Hash -ne '9C73C3BAE7ED48D44112A0F48E66742C00090BDB5BEF71D9D3C056C66E97B732') {
    throw 'Inno Setup compiler download hash mismatch.'
}
$destination = Join-Path $outputPath 'compiler'
$process = Start-Process -FilePath $download -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/CURRENTUSER','/NOICONS','/MERGETASKS=!fileassoc',('/DIR="' + $destination + '"')) -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath (Join-Path $destination 'ISCC.exe'))) {
    throw "Inno Setup compiler installation failed: $($process.ExitCode)"
}
