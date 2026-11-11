# Masaüstüne iki kısayol oluşturur: "YanEkran" ve "YanEkran (FIFA modu)".
# Başlangıç (Startup) klasörüne HİÇBİR ŞEY yazmaz: program Windows ile otomatik açılmaz.
param(
    [string]$ExePath = (Join-Path $PSScriptRoot "..\src\TelefonPaneli.App\bin\Release\net10.0-windows10.0.19041.0\YanEkran.exe")
)

$exe = (Resolve-Path $ExePath -ErrorAction Stop).Path
$desktop = [Environment]::GetFolderPath('Desktop')
$shell = New-Object -ComObject WScript.Shell

function New-Shortcut([string]$name, [string]$arguments) {
    $lnk = $shell.CreateShortcut((Join-Path $desktop "$name.lnk"))
    $lnk.TargetPath = $exe
    $lnk.Arguments = $arguments
    $lnk.WorkingDirectory = Split-Path $exe
    $lnk.IconLocation = "$exe,0"
    $lnk.Save()
    Write-Host "Oluşturuldu: $name"
}

# Eski adla oluşturulmuş kısayolları kaldır
foreach ($old in "Telefon Paneli", "Telefon Paneli (FIFA modu)") {
    $p = Join-Path $desktop "$old.lnk"
    if (Test-Path $p) { Remove-Item $p; Write-Host "Kaldırıldı: $old" }
}

New-Shortcut "YanEkran" ""
New-Shortcut "YanEkran (FIFA modu)" "--fifa"
