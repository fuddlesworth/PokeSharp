# PowerShell script to convert all pokeemerald maps to Tiled format
# Usage: .\convert_all_maps.ps1 -PokeemeraldDir "C:\path\to\pokeemerald" -MapOutputDir "C:\path\to\PokeSharp.Game\Assets\Data\Maps" -TilesetOutputDir "C:\path\to\PokeSharp.Game\Assets\Tilesets" [-MaxParallelJobs 4]

param(
    [Parameter(Mandatory=$true)]
    [string]$PokeemeraldDir,
    
    [Parameter(Mandatory=$true)]
    [string]$MapOutputDir,
    
    [Parameter(Mandatory=$true)]
    [string]$TilesetOutputDir,
    
    [Parameter(Mandatory=$false)]
    [int]$MaxParallelJobs = 4
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$convertMapScript = Join-Path $scriptDir "convert_map_8x8.py"
$convertTilesetScript = Join-Path $scriptDir "convert_tileset_8x8.py"

function New-NormalizedTilesetLookup {
    param([string]$BaseDir)

    $lookup = @{}
    if (Test-Path $BaseDir) {
        Get-ChildItem -Path $BaseDir -Directory | ForEach-Object {
            $key = ($_.Name -replace '[^a-z0-9]', '')
            if (-not $lookup.ContainsKey($key)) {
                $lookup[$key] = $_.FullName
            }
        }
    }
    return $lookup
}

# Validate paths
if (-not (Test-Path $PokeemeraldDir)) {
    Write-Host "[ERROR] Pokeemerald directory not found: $PokeemeraldDir" -ForegroundColor Red
    exit 1
}

$layoutsDir = Join-Path $PokeemeraldDir "data\layouts"
if (-not (Test-Path $layoutsDir)) {
    Write-Host "[ERROR] Layouts directory not found: $layoutsDir" -ForegroundColor Red
    exit 1
}

# Create output directories
New-Item -ItemType Directory -Path $MapOutputDir -Force | Out-Null
New-Item -ItemType Directory -Path $TilesetOutputDir -Force | Out-Null

$primaryTilesetsDir = Join-Path $PokeemeraldDir "data\tilesets\primary"
$secondaryTilesetsDir = Join-Path $PokeemeraldDir "data\tilesets\secondary"
$primaryTilesetLookup = New-NormalizedTilesetLookup $primaryTilesetsDir
$secondaryTilesetLookup = New-NormalizedTilesetLookup $secondaryTilesetsDir

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Pokeemerald Map Converter" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Pokeemerald dir: $PokeemeraldDir" -ForegroundColor Gray
Write-Host "Map output dir:  $MapOutputDir" -ForegroundColor Gray
Write-Host "Tileset output:  $TilesetOutputDir" -ForegroundColor Gray
Write-Host "Max parallel jobs: $MaxParallelJobs" -ForegroundColor Gray
Write-Host ""

# Function to process a single map
function Process-Map {
    param(
        [string]$LayoutFile,
        [string]$PokeemeraldDir,
        [string]$MapOutputDir,
        [string]$TilesetOutputDir,
        [string]$ConvertMapScript,
        [string]$ConvertTilesetScript
    )
    
    $result = @{
        MapName = ""
        Success = $false
        Error = $null
        Skipped = $false
    }
    
    try {
        $layoutDir = Split-Path -Parent $LayoutFile
        $mapName = Split-Path -Leaf $layoutDir
        
        $result.MapName = $mapName
        
        # Read layout.json to get tileset info
        $layoutJson = Get-Content $LayoutFile | ConvertFrom-Json
        $primaryTilesetName = $layoutJson.primary_tileset
        $secondaryTilesetName = $layoutJson.secondary_tileset
        
        if (-not $primaryTilesetName) {
            $result.Skipped = $true
            $result.Error = "No primary tileset specified"
            return $result
        }
        
        # Determine tileset directories
        $primaryTilesetDir = Join-Path $PokeemeraldDir "data\tilesets\primary\$primaryTilesetName"
        if (-not (Test-Path $primaryTilesetDir)) {
            $result.Skipped = $true
            $result.Error = "Primary tileset directory not found: $primaryTilesetDir"
            return $result
        }
        
        $secondaryTilesetDir = $null
        if ($secondaryTilesetName) {
            $secondaryTilesetDir = Join-Path $PokeemeraldDir "data\tilesets\secondary\$secondaryTilesetName"
            if (-not (Test-Path $secondaryTilesetDir)) {
                $secondaryTilesetDir = $null
            }
        }
        
        # Create map-specific tileset directory
        $mapTilesetDir = Join-Path $TilesetOutputDir $mapName
        New-Item -ItemType Directory -Path $mapTilesetDir -Force | Out-Null
        
        # Extract primary tileset directly to map folder with palettes applied
        $primaryTilesetJsonDest = Join-Path $mapTilesetDir "$primaryTilesetName.json"
        $primaryTilesetArgs = @(
            $ConvertTilesetScript,
            $primaryTilesetDir,
            $primaryTilesetJsonDest,
            "--apply-palettes",
            "--tile-offset", "0"
        )
        
        $tilesetResult = & python $primaryTilesetArgs 2>&1
        if ($LASTEXITCODE -ne 0) {
            $result.Error = "Failed to convert primary tileset: $tilesetResult"
            return $result
        }
        
        # Extract secondary tileset if present
        $secondaryTilesetJson = $null
        if ($secondaryTilesetDir) {
            $secondaryTilesetJsonDest = Join-Path $mapTilesetDir "$secondaryTilesetName.json"
            $secondaryTilesetArgs = @(
                $ConvertTilesetScript,
                $secondaryTilesetDir,
                $secondaryTilesetJsonDest,
                "--apply-palettes",
                "--tile-offset", "512"
            )
            
            $tilesetResult = & python $secondaryTilesetArgs 2>&1
            if ($LASTEXITCODE -ne 0) {
                $result.Error = "Failed to convert secondary tileset: $tilesetResult"
                return $result
            }
            
            $secondaryTilesetJson = "$mapName/$secondaryTilesetName.json"
        }
        
        # Calculate relative paths for tileset JSON references (from map to its tileset directory)
        $mapOutputPath = Join-Path $MapOutputDir "$mapName.json"
        $mapOutputUri = [System.Uri]([System.IO.Path]::GetFullPath($MapOutputDir) + '\')
        $mapTilesetUri = [System.Uri]([System.IO.Path]::GetFullPath($mapTilesetDir) + '\')
        $relativeUri = $mapOutputUri.MakeRelativeUri($mapTilesetUri)
        $relativeToMapOutput = $relativeUri.ToString() -Replace '\\', '/' -Replace '/$', ''
        
        $primaryTilesetJson = "$relativeToMapOutput/$primaryTilesetName.json"
        
        # Build Python command for map conversion
        $pythonArgs = @(
            $ConvertMapScript,
            $LayoutFile,
            $primaryTilesetDir,
            $mapOutputPath,
            "--primary-tileset-json", $primaryTilesetJson
        )
        
        if ($secondaryTilesetDir) {
            $pythonArgs += "--secondary-tileset-dir"
            $pythonArgs += $secondaryTilesetDir
            if ($secondaryTilesetJson) {
                $pythonArgs += "--secondary-tileset-json"
                $pythonArgs += "$relativeToMapOutput/$secondaryTilesetName.json"
            }
        }
        
        $mapResult = & python $pythonArgs 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            $result.Success = $true
        } else {
            $result.Error = "Map conversion failed: $mapResult"
        }
        
    } catch {
        $result.Error = "Exception: $_"
    }
    
    return $result
}

# Read master layouts.json file
Write-Host "[STEP 1] Reading layouts from master file..." -ForegroundColor Yellow
$masterLayoutsFile = Join-Path $layoutsDir "layouts.json"
if (-not (Test-Path $masterLayoutsFile)) {
    Write-Host "[ERROR] Master layouts.json not found: $masterLayoutsFile" -ForegroundColor Red
    exit 1
}

$masterLayouts = Get-Content $masterLayoutsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$allLayouts = $masterLayouts.layouts
Write-Host "Found $($allLayouts.Count) layouts in master file" -ForegroundColor Gray
Write-Host ""

# Process maps in parallel
Write-Host "[STEP 2] Converting maps (parallel, max $MaxParallelJobs jobs)..." -ForegroundColor Yellow
Write-Host ""

$successCount = 0
$failCount = 0
$skipCount = 0
$jobs = @()
$jobIndex = 0

# Create jobs for parallel processing
foreach ($layout in $allLayouts) {
    # Wait if we've reached max parallel jobs
    while ($jobs.Count -ge $MaxParallelJobs) {
        $finishedJobs = @($jobs | Where-Object { $_.State -eq "Completed" -or $_.State -eq "Failed" })
        foreach ($job in $finishedJobs) {
            $result = Receive-Job -Job $job
            Remove-Job -Job $job
            
            $mapName = $result.MapName
            if ($result.Skipped) {
                Write-Host "[SKIP] $mapName - $($result.Error)" -ForegroundColor Yellow
                $skipCount++
            } elseif ($result.Success) {
                Write-Host "[OK] $mapName - Converted successfully" -ForegroundColor Green
                $successCount++
            } else {
                Write-Host "[ERROR] $mapName - $($result.Error)" -ForegroundColor Red
                $failCount++
            }
        }
        $jobs = @($jobs | Where-Object { $_.State -ne "Completed" -and $_.State -ne "Failed" })
        Start-Sleep -Milliseconds 100
    }
    
    # Start new job (inline function code since jobs can't access functions directly)
    $job = Start-Job -ScriptBlock {
        param($Layout, $PokeemeraldDir, $MapOutputDir, $TilesetOutputDir, $ConvertMapScript, $ConvertTilesetScript, $LayoutsDir, $PrimaryLookup, $SecondaryLookup)
        
        $result = @{
            MapName = ""
            Success = $false
            Error = $null
            Skipped = $false
        }
        
        try {
            function Normalize-TilesetKey {
                param($Name)
                if (-not $Name) { return $null }
                return ($Name -replace '[^A-Za-z0-9]', '').ToLower()
            }

            # Extract map name from layout name (e.g., "LittlerootTown_Layout" -> "LittlerootTown")
            $mapName = $Layout.name -replace "_Layout$", ""
            
            $result.MapName = $mapName
            
            # Get layout info from the layout object
            $primaryTilesetName = $Layout.primary_tileset
            $secondaryTilesetName = $Layout.secondary_tileset
            
            # Strip "gTileset_" prefix if present (layout.json uses "gTileset_General" but directory is "general")
            if ($primaryTilesetName -like "gTileset_*") {
                $primaryTilesetName = $primaryTilesetName -replace "^gTileset_", ""
            }
            if ($secondaryTilesetName -like "gTileset_*") {
                $secondaryTilesetName = $secondaryTilesetName -replace "^gTileset_", ""
            }
            
            if (-not $primaryTilesetName) {
                $result.Skipped = $true
                $result.Error = "No primary tileset specified"
                return $result
            }
            
            # Determine tileset directories (case-insensitive, underscore-insensitive matching)
            $primaryTilesetDir = $null
            $primaryKey = Normalize-TilesetKey $primaryTilesetName
            if ($primaryKey -and $PrimaryLookup.ContainsKey($primaryKey)) {
                $primaryTilesetDir = $PrimaryLookup[$primaryKey]
            } else {
                $primaryTilesetDir = Join-Path $PokeemeraldDir "data\tilesets\primary\$primaryTilesetName"
                if (-not (Test-Path $primaryTilesetDir)) {
                    $primaryTilesetDir = Join-Path $PokeemeraldDir "data\tilesets\primary\$($primaryTilesetName.ToLower())"
                }
            }
            if (-not (Test-Path $primaryTilesetDir)) {
                $result.Skipped = $true
                $result.Error = "Primary tileset directory not found: $primaryTilesetDir"
                return $result
            }
            
            $secondaryTilesetDir = $null
            if ($secondaryTilesetName) {
                $secondaryKey = Normalize-TilesetKey $secondaryTilesetName
                if ($secondaryKey -and $SecondaryLookup.ContainsKey($secondaryKey)) {
                    $secondaryTilesetDir = $SecondaryLookup[$secondaryKey]
                } else {
                    $secondaryTilesetDir = Join-Path $PokeemeraldDir "data\tilesets\secondary\$secondaryTilesetName"
                    if (-not (Test-Path $secondaryTilesetDir)) {
                        $secondaryTilesetDir = Join-Path $PokeemeraldDir "data\tilesets\secondary\$($secondaryTilesetName.ToLower())"
                    }
                    if (-not (Test-Path $secondaryTilesetDir)) {
                        $secondaryTilesetDir = $null
                    }
                }
            }

            $primaryTilesetFolderName = (Split-Path $primaryTilesetDir -Leaf)
            $primaryTilesetFolderKey = $primaryTilesetFolderName.ToLower()
            
            # Extract primary tileset to shared location (organized by tileset name, lowercase for consistency)
            $primaryTilesetOutputDir = Join-Path $TilesetOutputDir $primaryTilesetFolderKey
            New-Item -ItemType Directory -Path $primaryTilesetOutputDir -Force | Out-Null
            $primaryTilesetJsonDest = Join-Path $primaryTilesetOutputDir "$primaryTilesetFolderKey.json"
            
            $primaryTilesetArgs = @(
                $ConvertTilesetScript,
                $primaryTilesetDir,
                $primaryTilesetJsonDest,
                "--apply-palettes",
                "--tile-offset", "0"
            )
            
            $tilesetResult = & python $primaryTilesetArgs 2>&1
            if ($LASTEXITCODE -ne 0) {
                $result.Error = "Failed to convert primary tileset: $tilesetResult"
                return $result
            }
            
            # Extract secondary tileset if present
            $secondaryTilesetJson = $null
            if ($secondaryTilesetDir) {
                $secondaryTilesetFolderName = Split-Path $secondaryTilesetDir -Leaf
                $secondaryTilesetFolderKey = $secondaryTilesetFolderName.ToLower()
                $secondaryTilesetOutputDir = Join-Path $TilesetOutputDir $secondaryTilesetFolderKey
                New-Item -ItemType Directory -Path $secondaryTilesetOutputDir -Force | Out-Null
                $secondaryTilesetJsonDest = Join-Path $secondaryTilesetOutputDir "$secondaryTilesetFolderKey.json"
                
                $secondaryTilesetArgs = @(
                    $ConvertTilesetScript,
                    $secondaryTilesetDir,
                    $secondaryTilesetJsonDest,
                    "--apply-palettes",
                    "--tile-offset", "512"
                )
                
                $tilesetResult = & python $secondaryTilesetArgs 2>&1
                if ($LASTEXITCODE -ne 0) {
                    $result.Error = "Failed to convert secondary tileset: $tilesetResult"
                    return $result
                }
                
                # Calculate relative path from Maps directory to secondary tileset
                $mapOutputUri = [System.Uri]([System.IO.Path]::GetFullPath($MapOutputDir) + '\')
                $secondaryTilesetUri = [System.Uri]([System.IO.Path]::GetFullPath($secondaryTilesetOutputDir) + '\')
                $relativeUri = $mapOutputUri.MakeRelativeUri($secondaryTilesetUri)
                $relativeToMapOutput = $relativeUri.ToString() -Replace '\\', '/' -Replace '/$', ''
                $secondaryTilesetJson = "$relativeToMapOutput/$secondaryTilesetFolderKey.json"
            }
            
            # Calculate relative paths for tileset JSON references (from Maps directory to tileset directory)
            $mapOutputPath = Join-Path $MapOutputDir "$mapName.json"
            $mapOutputUri = [System.Uri]([System.IO.Path]::GetFullPath($MapOutputDir) + '\')
            $primaryTilesetUri = [System.Uri]([System.IO.Path]::GetFullPath($primaryTilesetOutputDir) + '\')
            $relativeUri = $mapOutputUri.MakeRelativeUri($primaryTilesetUri)
            $relativeToMapOutput = $relativeUri.ToString() -Replace '\\', '/' -Replace '/$', ''
            
            $primaryTilesetJson = "$relativeToMapOutput/$primaryTilesetFolderKey.json"
            
            # Create temporary layout.json file for the map conversion script
            # Extract directory from blockdata_filepath (e.g., "data/layouts/PetalburgCity/map.bin" -> "PetalburgCity")
            $blockdataPath = $Layout.blockdata_filepath
            $layoutDirName = Split-Path (Split-Path $blockdataPath -Parent) -Leaf
            $layoutDir = Join-Path $LayoutsDir $layoutDirName
            $tempLayoutFile = Join-Path $layoutDir "layout.json"
            
            # Create layout.json if it doesn't exist
            if (-not (Test-Path $tempLayoutFile)) {
                $layoutJson = @{
                    width = $Layout.width
                    height = $Layout.height
                    primary_tileset = $Layout.primary_tileset
                    secondary_tileset = $Layout.secondary_tileset
                    border_filepath = $Layout.border_filepath
                    blockdata_filepath = $Layout.blockdata_filepath
                } | ConvertTo-Json
                New-Item -ItemType Directory -Path $layoutDir -Force | Out-Null
                $layoutJson | Out-File -FilePath $tempLayoutFile -Encoding UTF8
            }
            
            # Build Python command for map conversion
            $pythonArgs = @(
                $ConvertMapScript,
                $tempLayoutFile,
                $primaryTilesetDir,
                $mapOutputPath,
                "--primary-tileset-json", $primaryTilesetJson
            )
            
            if ($secondaryTilesetDir) {
                $pythonArgs += "--secondary-tileset-dir"
                $pythonArgs += $secondaryTilesetDir
                if ($secondaryTilesetJson) {
                    $pythonArgs += "--secondary-tileset-json"
                    $pythonArgs += $secondaryTilesetJson
                }
            }
            
            $mapResult = & python $pythonArgs 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                $result.Success = $true
            } else {
                $result.Error = "Map conversion failed: $mapResult"
            }
            
        } catch {
            $result.Error = "Exception: $_"
        }
        
        return $result
    } -ArgumentList $layout, $PokeemeraldDir, $MapOutputDir, $TilesetOutputDir, $convertMapScript, $convertTilesetScript, $layoutsDir, $primaryTilesetLookup, $secondaryTilesetLookup
    
    $jobs = @($jobs) + @($job)
    $jobIndex++
    
    if ($jobIndex % 10 -eq 0) {
        Write-Host "  Started $jobIndex / $($allLayouts.Count) jobs..." -ForegroundColor Gray
    }
}

# Wait for all remaining jobs to complete
Write-Host "  Waiting for remaining jobs to complete..." -ForegroundColor Gray
while ($jobs.Count -gt 0) {
    $finishedJobs = @($jobs | Where-Object { $_.State -eq "Completed" -or $_.State -eq "Failed" })
    foreach ($job in $finishedJobs) {
        $result = Receive-Job -Job $job
        Remove-Job -Job $job
        
        $mapName = $result.MapName
        if ($result.Skipped) {
            Write-Host "[SKIP] $mapName - $($result.Error)" -ForegroundColor Yellow
            $skipCount++
        } elseif ($result.Success) {
            Write-Host "[OK] $mapName - Converted successfully" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "[ERROR] $mapName - $($result.Error)" -ForegroundColor Red
            $failCount++
        }
    }
    $jobs = @($jobs | Where-Object { $_.State -ne "Completed" -and $_.State -ne "Failed" })
    if ($jobs.Count -gt 0) {
        Start-Sleep -Milliseconds 500
    }
}

Write-Host ""

# Summary
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Conversion Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  [OK] Success:    $successCount" -ForegroundColor Green
Write-Host "  [ERROR] Failed:  $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Gray" })
Write-Host "  [SKIP] Skipped:  $skipCount" -ForegroundColor $(if ($skipCount -gt 0) { "Yellow" } else { "Gray" })
Write-Host ""

if ($failCount -eq 0) {
    Write-Host "[OK] All maps converted successfully!" -ForegroundColor Green
} else {
    Write-Host "[WARN] Some maps failed to convert. Check errors above." -ForegroundColor Yellow
}

