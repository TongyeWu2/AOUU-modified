param(
    [string]$ConfigPath = (Join-Path $env:LOCALAPPDATA "AOUU\config.json"),
    [string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string]$OutputPath = (Join-Path $ProjectRoot "assets\default_config.json")
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "User config was not found: $ConfigPath"
}

$projectRootFull = [System.IO.Path]::GetFullPath($ProjectRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
$localTemplateRoot = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA "AOUU\templates\defaults")).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)

function Convert-BundledPath {
    param([string]$PathValue)

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        return $PathValue
    }

    if (-not [System.IO.Path]::IsPathRooted($PathValue)) {
        return $PathValue.Replace("\", "/")
    }

    $fullPath = [System.IO.Path]::GetFullPath($PathValue)
    if ($fullPath.StartsWith($projectRootFull, [System.StringComparison]::OrdinalIgnoreCase)) {
        return Get-RelativePath $projectRootFull $fullPath
    }

    if ($fullPath.StartsWith($localTemplateRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $fileName = [System.IO.Path]::GetFileName($fullPath)
        return "templates/defaults/$fileName"
    }

    return $PathValue
}

function Get-RelativePath {
    param(
        [string]$BasePath,
        [string]$FullPath
    )

    $baseUri = [System.Uri]::new(($BasePath.TrimEnd("\", "/") + [System.IO.Path]::DirectorySeparatorChar))
    $fullUri = [System.Uri]::new($FullPath)
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($fullUri).ToString()).Replace("\", "/")
}

function Convert-ConfigObject {
    param($Value)

    if ($null -eq $Value) {
        return
    }

    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        foreach ($item in $Value) {
            Convert-ConfigObject $item
        }
        return
    }

    if ($Value -isnot [pscustomobject]) {
        return
    }

    foreach ($property in $Value.PSObject.Properties) {
        if ($property.Value -is [string] -and $property.Name -match "(Path|ImagePath|MusicPath)\d*$") {
            $property.Value = Convert-BundledPath $property.Value
            continue
        }

        Convert-ConfigObject $property.Value
    }
}

$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
Convert-ConfigObject $config

$outputDirectory = Split-Path -Parent $OutputPath
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$config | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
Write-Host "Exported bundled default config to $OutputPath"
