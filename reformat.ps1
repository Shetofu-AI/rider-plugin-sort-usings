param(
    [Parameter(Mandatory, Position = 0)][string]$Target,          # .sln / .slnx / .csproj
    [string]$CleanupProfile = 'Built-in: Full Cleanup',           # SortUsingsByLength включён только в Full
    [string]$Settings,                                            # свой .DotSettings, иначе solution shared
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$id = 'Shetofu.Rider'

if (-not (Get-Command jb -ErrorAction SilentlyContinue)) {
    dotnet tool install -g JetBrains.ReSharper.GlobalTools
}

# плагин должен быть собран против тех же сборок, что и у CLT
$clt = (Get-ChildItem "$env:USERPROFILE\.dotnet\tools\.store\jetbrains.resharper.globaltools" `
    -Recurse -Filter JetBrains.ReSharper.Psi.dll | Sort-Object FullName)[-1].DirectoryName

# без совпадающей Wave-зависимости CLT молча не грузит расширение: 2026.2.0 -> 262.0
$cltVersion = [version](($clt -split '\\' | Where-Object { $_ -as [version] })[-1])
$wave = '{0}{1}.0' -f $cltVersion.Major.ToString().Substring(2), $cltVersion.Minor

$feed = Join-Path $root 'bin\feed'
dotnet build "$root\Shetofu.Rider.csproj" -c Release -p:ReSharperHost=$clt --nologo -v q
dotnet pack "$root\Shetofu.Rider.csproj" -c Release --no-build --nologo -v q -o $feed `
    -p:NuspecFile="$root\Shetofu.Rider.nuspec" -p:NuspecBasePath=$root -p:NuspecProperties="wave=$wave"

# CLT кеширует расширение по id+version, версия не меняется — выкидываем прошлую распаковку
Get-Item "$env:LOCALAPPDATA\JetBrains\Shared\vAny\DeployedPackagesExpand\$id.1.0.0",
         "$env:LOCALAPPDATA\JetBrains\~\NugetLocalRestore\$id.1.0.0" -ErrorAction SilentlyContinue |
    Remove-Item -Recurse -Force

$jbArgs = @($Target, "--eXtensions=$id", "--source=$feed", "--profile=$CleanupProfile")
if ($Settings) { $jbArgs += "--settings=$Settings" }
if ($NoBuild) { $jbArgs += '--no-build' }
jb cleanupcode @jbArgs
