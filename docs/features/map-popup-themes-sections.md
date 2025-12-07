# Map Popup Themes and Sections Integration

**Date:** December 5, 2025
**Feature:** Entity Framework integration for popup themes and map sections
**Status:** ✅ COMPLETE

---

## Overview

This feature wires up popup themes and map sections (MAPSEC) into Entity Framework Core, allowing the game to properly retrieve the correct theme and section name for map popups based on region data from pokeemerald.

## Architecture

### Entity Classes

#### PopupTheme (`GameData/Entities/PopupTheme.cs`)
Stores popup theme definitions (wood, marble, stone, brick, underwater, stone2).

**Properties:**
- `Id` - Theme identifier (e.g., "wood", "marble")
- `Name` - Display name
- `Description` - Usage description
- `Background` - Background asset ID
- `Outline` - Outline asset ID
- `UsageCount` - Statistics on usage
- `MapSections` - Navigation property to sections using this theme

#### MapSection (`GameData/Entities/MapSection.cs`)
Stores map section (MAPSEC) definitions from pokeemerald.

**Properties:**
- `Id` - MAPSEC identifier (e.g., "MAPSEC_LITTLEROOT_TOWN")
- `Name` - Display name (e.g., "LITTLEROOT TOWN")
- `ThemeId` - Foreign key to PopupTheme
- `X`, `Y`, `Width`, `Height` - Region map coordinates (optional)
- `Theme` - Navigation property to the theme

### Data Service

#### MapPopupService (`GameData/Services/MapPopupService.cs`)
Provides cached O(1) lookups for themes and sections.

**Key Methods:**
- `GetTheme(themeId)` - Get theme by ID
- `GetSection(sectionId)` - Get section by ID
- `GetThemeForSection(sectionId)` - Get theme for a section
- `GetPopupDisplayInfo(sectionId)` - Get complete popup info (section name + theme assets)
- `PreloadAllAsync()` - Preload all data into cache

### Data Loading

The `GameDataLoader` now loads themes and sections from JSON files:
- **Themes:** `Assets/Data/Maps/Popups/Themes/*.json`
- **Sections:** `Assets/Data/Maps/Sections/*.json`

Data is loaded into the in-memory EF Core database during game initialization.

---

## Usage Examples

### Example 1: Get Popup Info for a Map Section

```csharp
using MonoBallFramework.Game.GameData.Services;

public class MyMapSystem
{
    private readonly MapPopupService _mapPopupService;

    public MyMapSystem(MapPopupService mapPopupService)
    {
        _mapPopupService = mapPopupService;
    }

    public void ShowMapPopup(string regionSectionId)
    {
        // Get complete popup display info
        var popupInfo = _mapPopupService.GetPopupDisplayInfo("MAPSEC_LITTLEROOT_TOWN");

        if (popupInfo != null)
        {
            Console.WriteLine($"Section: {popupInfo.SectionName}");
            Console.WriteLine($"Theme: {popupInfo.ThemeId}");
            Console.WriteLine($"Background: {popupInfo.BackgroundAssetId}");
            Console.WriteLine($"Outline: {popupInfo.OutlineAssetId}");
        }
    }
}
```

**Output:**
```
Section: LITTLEROOT TOWN
Theme: wood
Background: wood
Outline: wood_outline
```

### Example 2: Get Theme Details

```csharp
using MonoBallFramework.Game.GameData.Entities;

public void DisplayThemeInfo(string themeId)
{
    PopupTheme? theme = _mapPopupService.GetTheme("marble");

    if (theme != null)
    {
        Console.WriteLine($"Theme: {theme.Name}");
        Console.WriteLine($"Description: {theme.Description}");
        Console.WriteLine($"Background: {theme.Background}");
        Console.WriteLine($"Outline: {theme.Outline}");
        Console.WriteLine($"Used by {theme.UsageCount} sections");
    }
}
```

**Output:**
```
Theme: Marble
Description: Marble frame for major cities
Background: marble
Outline: marble_outline
Used by 9 sections
```

### Example 3: Get All Sections for a Theme

```csharp
public async Task ListSectionsByTheme(string themeId)
{
    var sections = await _mapPopupService.GetSectionsByThemeAsync("underwater");

    foreach (var section in sections)
    {
        Console.WriteLine($"- {section.Name} ({section.Id})");
    }
}
```

**Output:**
```
- ROUTE 105 (MAPSEC_ROUTE_105)
- ROUTE 106 (MAPSEC_ROUTE_106)
- ROUTE 107 (MAPSEC_ROUTE_107)
...
```

### Example 4: Preload Data for Performance

```csharp
public async Task InitializeAsync()
{
    // Preload all themes and sections into cache for O(1) lookups
    await _mapPopupService.PreloadAllAsync();

    // Now all lookups will be instant from cache
    var popupInfo = _mapPopupService.GetPopupDisplayInfo("MAPSEC_ROUTE_101");
}
```

---

## Integration with Map Popup Display

The `Engine.Scenes.Services.MapPopupService` now uses the new `GameData.Services.MapPopupService` to get the correct theme:

```csharp
private void ShowPopupForMap(int mapId, string displayName, string? regionName)
{
    // Get popup info from the data service
    PopupDisplayInfo? popupInfo = _mapPopupDataService.GetPopupDisplayInfo(regionName);

    if (popupInfo != null)
    {
        // Get definitions from registry using the theme's asset IDs
        backgroundDef = _popupRegistry.GetBackground(popupInfo.BackgroundAssetId);
        outlineDef = _popupRegistry.GetOutline(popupInfo.OutlineAssetId);

        // Use the section name from the database
        displayName = popupInfo.SectionName;
    }
    else
    {
        // Fallback to default wood theme
        backgroundDef = _popupRegistry.GetDefaultBackground();
        outlineDef = _popupRegistry.GetDefaultOutline();
    }

    // Create and display popup...
}
```

---

## Data Flow

```
Map Definition (Tiled JSON)
  ├─ RegionMapSection: "MAPSEC_LITTLEROOT_TOWN"
  └─ DisplayName: "Littleroot Town"
       ↓
MapPopupDataService.GetPopupDisplayInfo("MAPSEC_LITTLEROOT_TOWN")
       ↓
1. Query MapSection table → Get section data
   └─ Name: "LITTLEROOT TOWN"
   └─ ThemeId: "wood"
       ↓
2. Query PopupTheme table → Get theme data
   └─ Background: "wood"
   └─ Outline: "wood_outline"
       ↓
3. Return PopupDisplayInfo
   └─ SectionName: "LITTLEROOT TOWN"
   └─ BackgroundAssetId: "wood"
   └─ OutlineAssetId: "wood_outline"
   └─ ThemeId: "wood"
       ↓
4. PopupRegistry gets actual textures
   └─ GetBackground("wood") → Texture2D
   └─ GetOutline("wood_outline") → Texture2D
       ↓
5. MapPopupScene displays popup with correct theme
```

---

## Database Schema

### PopupThemes Table
| Column | Type | Description |
|--------|------|-------------|
| Id | string(50) | Primary key, theme ID |
| Name | string(100) | Display name |
| Description | string(500) | Usage description |
| Background | string(100) | Background asset ID |
| Outline | string(100) | Outline asset ID |
| UsageCount | int | Statistics |
| SourceMod | string(100) | Mod identifier (nullable) |
| Version | string(20) | Version tracking |

**Indexes:**
- `Name`
- `Background`
- `Outline`

### MapSections Table
| Column | Type | Description |
|--------|------|-------------|
| Id | string(100) | Primary key, MAPSEC ID |
| Name | string(100) | Display name |
| ThemeId | string(50) | Foreign key to PopupThemes |
| X | int? | Region map X coordinate |
| Y | int? | Region map Y coordinate |
| Width | int? | Region map width |
| Height | int? | Region map height |
| SourceMod | string(100) | Mod identifier (nullable) |
| Version | string(20) | Version tracking |

**Indexes:**
- `Name`
- `ThemeId`
- `(X, Y)` composite

**Relationships:**
- `MapSection.ThemeId` → `PopupTheme.Id` (one-to-many)

---

## Performance

### Caching Strategy
- **First Query:** Loads from EF Core in-memory database
- **Subsequent Queries:** O(1) lookup from `ConcurrentDictionary` cache
- **Preloading:** `PreloadAllAsync()` loads all data into cache on startup

### Benchmark Results (Expected)
- **Cold lookup:** ~10-50 microseconds (EF Core query)
- **Cached lookup:** ~0.1-1 microsecond (dictionary lookup)
- **Preload time:** ~1-5 milliseconds for all themes + sections

---

## Testing

### Verify Data Loading

```csharp
public async Task TestDataLoading(MapPopupService service)
{
    var stats = await service.GetStatisticsAsync();

    Console.WriteLine($"Themes loaded: {stats.TotalThemes}");
    Console.WriteLine($"Sections loaded: {stats.TotalSections}");
    Console.WriteLine($"Themes cached: {stats.ThemesCached}");
    Console.WriteLine($"Sections cached: {stats.SectionsCached}");
}
```

**Expected Output:**
```
Themes loaded: 6
Sections loaded: 213
Themes cached: 0
Sections cached: 0
```

### Verify Theme Assignment

```csharp
public void TestThemeAssignments()
{
    // Test different theme types
    AssertTheme("MAPSEC_LITTLEROOT_TOWN", "wood");
    AssertTheme("MAPSEC_SLATEPORT_CITY", "marble");
    AssertTheme("MAPSEC_GRANITE_CAVE", "stone");
    AssertTheme("MAPSEC_PETALBURG_CITY", "brick");
    AssertTheme("MAPSEC_ROUTE_105", "underwater");
    AssertTheme("MAPSEC_UNDERWATER_124", "stone2");
}

private void AssertTheme(string sectionId, string expectedTheme)
{
    var theme = _mapPopupService.GetThemeForSection(sectionId);
    Assert.Equal(expectedTheme, theme?.Id);
}
```

---

## Source Data

### Themes
Extracted from `pokeemerald/src/map_name_popup.c`:
- **wood** - Default wooden frame (143 sections)
- **marble** - Marble frame for cities (9 sections)
- **stone** - Stone frame for caves (29 sections)
- **brick** - Brick frame for some cities (4 sections)
- **underwater** - Water routes (17 sections)
- **stone2** - Deep underwater (11 sections)

### Sections
Extracted from:
1. `pokeemerald/src/data/region_map/region_map_sections.json` - Section definitions
2. `pokeemerald/src/map_name_popup.c` - Theme mappings

---

## Future Enhancements

### Potential Improvements
1. **Runtime theme switching** - Allow mods to override themes
2. **Dynamic theme loading** - Load themes from asset packs
3. **Theme variants** - Support seasonal or time-based variants
4. **Region-specific themes** - Different themes for Kanto vs Hoenn
5. **Custom section names** - Allow localization of section names

### Modding Support
The system already supports modding via:
- `SourceMod` property on entities
- Mod override in `GameDataLoader`
- Extensible loading from multiple directories

---

## Related Documentation

- **Popup System:** `docs/features/map-popups.md`
- **Popup Registry:** `MonoBallFramework.Game/Engine/Rendering/Popups/README.md`
- **Themes README:** `MonoBallFramework.Game/Assets/Data/Maps/Popups/Themes/README.md`
- **Sections README:** `MonoBallFramework.Game/Assets/Data/Maps/Sections/README.md`
- **Entity Framework:** `MonoBallFramework.Game/GameData/README.md` (if exists)

---

## Summary

✅ PopupTheme and MapSection entities created
✅ Entity Framework DbContext configured
✅ Data loading from JSON files implemented
✅ MapPopupService with caching created
✅ Service registered in DI container
✅ Integrated with map popup display system
✅ Logging added for debugging
✅ Performance optimized with caching

The system is now ready to display map popups with the correct theme based on region data from pokeemerald!



