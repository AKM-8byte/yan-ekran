# Sürüm paketlerini oluşturur: artifacts/YanEkran-<sürüm>-win-x64.zip (bağımsız) ve -kucuk.zip (.NET kurulu olanlar için).
# Kullanım: powershell -ExecutionPolicy Bypass -File scripts/package-release.ps1
# Sürüm numarası src/TelefonPaneli.App/TelefonPaneli.App.csproj içindeki <Version> değerinden okunur.
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Location $root

$csproj = 'src/TelefonPaneli.App/TelefonPaneli.App.csproj'
$version = ([xml](Get-Content $csproj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw '<Version> bulunamadı' }
if (-not (Test-Path 'tools/presentmon/PresentMon.exe')) { throw 'tools/presentmon/PresentMon.exe yok (bkz. tools/presentmon/README.md)' }
if (git status --porcelain) { Write-Warning 'Çalışma ağacında commit edilmemiş değişiklik var: exe sürüm bilgisi son commit kimliğini taşır.' }

$out = Join-Path $root 'artifacts'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
$common = @('-c', 'Release', '-r', 'win-x64', '-p:DebugType=None', '-p:DebugSymbols=false', '-p:SatelliteResourceLanguages=tr')

foreach ($kind in @(@{ Suffix = ''; SelfContained = 'true' }, @{ Suffix = '-kucuk'; SelfContained = 'false' })) {
    $dir = Join-Path $out "pkg$($kind.Suffix)\YanEkran"
    dotnet publish src/TelefonPaneli.App @common --self-contained $kind.SelfContained -o $dir | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish başarısız' }
    Copy-Item LICENSE (Join-Path $dir 'LICENSE.txt')
    Copy-Item packaging/PresentMon-LICENSE.txt (Join-Path $dir 'tools\presentmon\LICENSE.txt')
    Copy-Item scripts/create-shortcuts.ps1 (Join-Path $dir 'kisayol-olustur.ps1')
    $readme = (Get-Content packaging/OKU-BENI.txt -Raw -Encoding UTF8).Replace('{SURUM}', $version)
    [IO.File]::WriteAllText((Join-Path $dir 'OKU-BENI.txt'), $readme, (New-Object Text.UTF8Encoding($true)))
    $zip = Join-Path $out "YanEkran-$version-win-x64$($kind.Suffix).zip"
    Compress-Archive -Path $dir -DestinationPath $zip -CompressionLevel Optimal
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
    Write-Host ("{0}  {1}  ({2:N1} MB)" -f $hash, (Split-Path $zip -Leaf), ((Get-Item $zip).Length / 1MB))
}
