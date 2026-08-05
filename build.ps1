param(
    [string]$RiderHome = (Get-ChildItem 'C:\Program Files\JetBrains' -Directory -Filter 'JetBrains Rider*' |
        Sort-Object LastWriteTime | Select-Object -Last 1).FullName,
    [string]$PluginsDir  # по умолчанию берётся из product-info.json целевого Rider
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

if (-not $PluginsDir) {
    $dataDir = (Get-Content "$RiderHome\product-info.json" -Raw | ConvertFrom-Json).dataDirectoryName
    $PluginsDir = "$env:APPDATA\JetBrains\$dataDir\plugins"
}

dotnet build "$root\Shetofu.Rider.csproj" -c Release -p:RiderHome=$RiderHome

$target = Join-Path $PluginsDir 'shetofu-rider'
New-Item -ItemType Directory -Force -Path "$target\dotnet", "$target\lib" | Out-Null
Copy-Item "$root\bin\Release\Shetofu.Rider.dll" "$target\dotnet\" -Force

# запись обязана называться 'META-INF/plugin.xml'; Compress-Archive под Windows PowerShell 5.1
# пишет её через '\', и Rider такой плагин молча не грузит
$jar = "$target\lib\shetofu-rider.jar"
Remove-Item $jar -Force -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($jar, 'Create')
try {
    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $zip, "$root\META-INF\plugin.xml", 'META-INF/plugin.xml') | Out-Null
}
finally {
    $zip.Dispose()
}

Write-Output "Installed to $target. Restart Rider."
