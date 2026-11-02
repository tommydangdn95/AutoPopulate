# Publish a new AutoPopulate release to GitHub (the app picks it up the next time it is opened).
#
#   1. Set <Version> in App\App.csproj and commit.
#   2. Write the release notes (shown to users in the update popup) to a UTF-8 text file.
#   3. powershell -File tools\Publish-Release.ps1 -NotesFile notes.txt
#
# Add -PackageOnly to build the zip and .sha256 without creating the tag or the release.
param(
    [string]$NotesFile,
    [switch]$PackageOnly
)
$ErrorActionPreference = "Stop"
$repo = "tommydangdn95/AutoPopulate"
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

$version = ([xml](Get-Content "App\App.csproj" -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) { throw "Version not found in App\App.csproj" }
$tag = "v$version"
$zipName = "AutoPopulate-$tag-win-x64.zip"

if (-not $PackageOnly) {
    if (-not $NotesFile -or -not (Test-Path $NotesFile)) { throw "-NotesFile is required" }
    if (git status --porcelain) { throw "Working tree has uncommitted changes. Commit them first." }
}

# Package
if (Test-Path publish) { Remove-Item publish -Recurse -Force }
dotnet publish App\App.csproj -p:PublishProfile=Portable | Out-Null
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }
Push-Location publish
& "$env:SystemRoot\System32\tar.exe" -a -c -f $zipName AutoPopulate
Pop-Location
$zip = Join-Path $root "publish\$zipName"
$sha = Join-Path $root "publish\$zipName.sha256"
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash
[IO.File]::WriteAllText($sha, "$hash  $zipName`n", [Text.UTF8Encoding]::new($false))
Write-Output "Package: $zip"
Write-Output "SHA256 : $hash"
if ($PackageOnly) { return }

# Tag
git tag -a $tag -m "AutoPopulate $version"
git push origin HEAD
git push origin $tag

# GitHub release, using the credentials git already stores (never printed).
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$query = Join-Path $env:TEMP ("cred_" + [guid]::NewGuid() + ".txt")
[IO.File]::WriteAllText($query, "protocol=https`nhost=github.com`n`n", [Text.UTF8Encoding]::new($false))
$cred = cmd /c "git credential fill < `"$query`""
Remove-Item $query -Force
$token = ($cred | Where-Object { $_ -like "password=*" }) -replace "^password=", ""
if (-not $token) { throw "Could not read GitHub credentials from git." }
$headers = @{ Authorization = "Bearer $token"; Accept = "application/vnd.github+json"; "User-Agent" = "AutoPopulate-release" }

$payload = @{
    tag_name = $tag
    name     = "AutoPopulate $version"
    body     = [IO.File]::ReadAllText((Resolve-Path $NotesFile), [Text.Encoding]::UTF8)
} | ConvertTo-Json
$release = Invoke-RestMethod -Method Post -Headers $headers -Uri "https://api.github.com/repos/$repo/releases" `
    -ContentType "application/json; charset=utf-8" -Body ([Text.Encoding]::UTF8.GetBytes($payload))

foreach ($file in @($zip, $sha)) {
    $name = [Uri]::EscapeDataString([IO.Path]::GetFileName($file))
    $type = if ($file -like "*.zip") { "application/zip" } else { "text/plain" }
    Invoke-RestMethod -Method Post -Headers $headers -ContentType $type -InFile $file -TimeoutSec 1800 `
        -Uri "https://uploads.github.com/repos/$repo/releases/$($release.id)/assets?name=$name" | Out-Null
    Write-Output "Uploaded: $([IO.Path]::GetFileName($file))"
}
Write-Output "Release: $($release.html_url)"
