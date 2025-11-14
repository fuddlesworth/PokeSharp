# PowerShell script to apply palettes to all tilesets
# Usage: .\apply_palettes_all.ps1 -PokeemeraldDir "C:\path\to\pokeemerald" -OutputDir "C:\Users\nate0\RiderProjects\PokeSharp\PokeSharp.Game\Assets\Tilesets"

param(
    [Parameter(Mandatory=$true)]
    [string]$PokeemeraldDir,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputDir = "..\..\PokeSharp.Game\Assets\Tilesets"
)

$ErrorActionPreference = "Stop"

# Tilesets to process (primary and secondary)
$tilesets = @(
    @{ Type = "primary"; Name = "general"; Offset = 0 },
    @{ Type = "primary"; Name = "building"; Offset = 0 },
    @{ Type = "secondary"; Name = "petalburg"; Offset = 512 },
    @{ Type = "secondary"; Name = "brendans_mays_house"; Offset = 512 },
    @{ Type = "secondary"; Name = "lab"; Offset = 512 }
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$renderScript = Join-Path $scriptDir "render_tileset_with_palettes.py"

if (-not (Test-Path $renderScript)) {
    Write-Host "❌ Error: render_tileset_with_palettes.py not found at $renderScript" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $PokeemeraldDir)) {
    Write-Host "❌ Error: Pokeemerald directory not found: $PokeemeraldDir" -ForegroundColor Red
    exit 1
}

$outputPath = Resolve-Path $OutputDir -ErrorAction SilentlyContinue
if (-not $outputPath) {
    $outputPath = New-Item -ItemType Directory -Path $OutputDir -Force | Select-Object -ExpandProperty FullName
}

Write-Host "`n🎨 Applying palettes to all tilesets..." -ForegroundColor Cyan
Write-Host "   Pokeemerald dir: $PokeemeraldDir" -ForegroundColor Gray
Write-Host "   Output dir: $outputPath" -ForegroundColor Gray
Write-Host ""

$successCount = 0
$failCount = 0

foreach ($tileset in $tilesets) {
    $tilesetType = $tileset.Type
    $tilesetName = $tileset.Name
    $tileOffset = $tileset.Offset
    
    $sourceDir = Join-Path $PokeemeraldDir "data\tilesets\$tilesetType\$tilesetName"
    $tilesPng = Join-Path $sourceDir "tiles.png"
    $palettesDir = Join-Path $sourceDir "palettes"
    $metatilesBin = Join-Path $sourceDir "metatiles.bin"
    
    # Handle naming: brendans_mays_house -> brendans_mays_house_tiles.png
    $outputPng = Join-Path $outputPath "${tilesetName}_tiles.png"
    
    Write-Host "Processing: $tilesetName ($tilesetType)" -ForegroundColor Yellow
    
    # Check if source files exist
    if (-not (Test-Path $tilesPng)) {
        Write-Host "  ⚠️  Skipping: tiles.png not found at $tilesPng" -ForegroundColor Yellow
        $failCount++
        continue
    }
    
    if (-not (Test-Path $palettesDir)) {
        Write-Host "  ⚠️  Skipping: palettes directory not found at $palettesDir" -ForegroundColor Yellow
        $failCount++
        continue
    }
    
    if (-not (Test-Path $metatilesBin)) {
        Write-Host "  ⚠️  Skipping: metatiles.bin not found at $metatilesBin" -ForegroundColor Yellow
        $failCount++
        continue
    }
    
    # Build Python command
    $pythonArgs = @(
        $renderScript,
        $tilesPng,
        $palettesDir,
        $metatilesBin,
        $outputPng
    )
    
    if ($tileOffset -gt 0) {
        $pythonArgs += $tileOffset
    }
    
    try {
        # Run Python script
        $result = & python $pythonArgs 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ Applied palette to $tilesetName" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "  ❌ Failed to apply palette to $tilesetName" -ForegroundColor Red
            Write-Host "  Error: $result" -ForegroundColor Red
            $failCount++
        }
    } catch {
        Write-Host "  ❌ Error processing $tilesetName : $_" -ForegroundColor Red
        $failCount++
    }
    
    Write-Host ""
}

Write-Host "`n📊 Summary:" -ForegroundColor Cyan
Write-Host "   ✅ Success: $successCount" -ForegroundColor Green
Write-Host "   ❌ Failed:  $failCount" -ForegroundColor $(if ($failCount -gt 0) { "Red" } else { "Gray" })
Write-Host ""

if ($failCount -eq 0) {
    Write-Host "🎉 All palettes applied successfully!" -ForegroundColor Green
} else {
    Write-Host "⚠️  Some tilesets failed. Check the errors above." -ForegroundColor Yellow
}


