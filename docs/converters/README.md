# Pokeemerald to PokeSharp Data Converter Design

## Overview

This directory contains the complete design specifications for converting pokeemerald decompilation project data into PokeSharp-compatible JSON format.

## Files

1. **PokeemeraldConverterDesign.cs** - Complete C# interface and class definitions for the conversion system
2. **ImplementationGuide.md** - Detailed implementation strategy and technical specifications
3. **ExampleOutputSchemas.md** - Example JSON outputs showing conversion results
4. **README.md** - This file

## Quick Reference

### What Gets Converted

| Data Type | Source | Target | Priority |
|-----------|--------|--------|----------|
| Pokemon Species | C structs | JSON templates | HIGH |
| Moves | C structs | JSON definitions | HIGH |
| Maps | JSON + scripts | JSON + .csx | HIGH |
| Event Scripts | Pory/poryscript | C# scripts | MEDIUM |
| Graphics References | File paths | Asset manifest | MEDIUM |

### Conversion Pipeline

```
pokeemerald → Parsers → Converters → Validators → JSON Files
```

### Key Components

1. **IPokemonDataConverter** - Converts Pokemon species from C to JSON
2. **IMoveDataConverter** - Converts battle moves from C to JSON
3. **IMapDataConverter** - Converts maps from pokeemerald JSON to PokeSharp format
4. **IScriptConverter** - Converts pory scripts to C# (.csx)
5. **IGraphicsConverter** - Maps asset paths (no file conversion)
6. **IConversionPipeline** - Orchestrates the entire process

## Data Flow

### Pokemon Species Conversion

```
species_info.h (C struct)
    ↓ Parse C struct
EmeraldSpeciesInfo (C# object)
    ↓ Convert to template
PokemonSpeciesTemplate (C# object)
    ↓ Serialize to JSON
001_Bulbasaur.json
```

**Additional Data Sources:**
- `pokedex_entries.h` → Description text
- `level_up_learnsets.h` → Level-up moves
- `tmhm_learnsets.h` → TM/HM compatibility
- `egg_moves.h` → Egg moves
- `evolution.h` → Evolution methods

### Move Conversion

```
battle_moves.h (C struct)
    ↓ Parse BattleMove array
EmeraldBattleMove (C# object)
    ↓ Convert effects/flags
MoveDefinition (C# object)
    ↓ Serialize to JSON
053_Flamethrower.json
```

### Map Conversion

```
map.json (pokeemerald)
    ↓ Parse JSON
EmeraldMapJson (C# object)
    ↓ Convert events/warps
PokeSharpMapDefinition (C# object)
    ↓ Serialize to JSON
Route102/map.json
```

### Script Conversion

```
scripts.inc (pory)
    ↓ Parse pory syntax
PoryScriptFile (AST)
    ↓ Convert to C#
CSharpScriptFile
    ↓ Generate .csx
Route102/scripts.csx
```

## Implementation Phases

### Phase 1: Foundation (Week 1)
- [ ] Implement C struct parser
- [ ] Build constant resolver
- [ ] Create validation framework

### Phase 2: Core Converters (Week 2-3)
- [ ] Pokemon species converter
- [ ] Move data converter
- [ ] Map data converter

### Phase 3: Advanced Features (Week 4)
- [ ] Script converter (using porycon)
- [ ] Graphics manifest generator
- [ ] Tiled format support

### Phase 4: Testing & Refinement (Week 5)
- [ ] Validate all conversions
- [ ] Handle edge cases
- [ ] Performance optimization
- [ ] Documentation

## Key Design Decisions

### 1. Why Not Binary Parsing?

pokeemerald uses C source files that compile to GBA ROM. We parse the **source code** (C/JSON), not the compiled binary. This is:
- More reliable (source is canonical)
- Easier to maintain
- Provides better error messages

### 2. Graphics Strategy

**We do NOT convert graphics files.** We only:
- Map file paths from pokeemerald to PokeSharp
- Generate asset manifest JSON
- Keep original .png files

Rationale: pokeemerald already has extracted .png files. No need to re-process.

### 3. Script Conversion Approach

**Leverage porycon parser** (already in project):
- porycon parses .inc files to AST
- We convert AST to C# syntax
- Generate .csx files for PokeSharp scripting

Rationale: Don't reinvent pory parser. Use existing tooling.

### 4. Map Format Compatibility

**Target Tiled format when possible:**
- PokeSharp already uses Tiled map loader
- pokeemerald layout data can map to Tiled layers
- Fallback to custom JSON if Tiled incompatible

Rationale: Leverage existing PokeSharp infrastructure.

### 5. Constant Resolution

**Build constant lookup tables from pokeemerald headers:**
- Parse `/include/constants/*.h` files
- Map enum names to values
- Resolve in conversion process

Rationale: Ensures accurate data without hardcoding.

## Expected Output Structure

```
PokeSharp.Game/Assets/
├── Data/
│   ├── Pokemon/
│   │   ├── 001_Bulbasaur.json
│   │   ├── 002_Ivysaur.json
│   │   └── ... (386 files)
│   ├── Moves/
│   │   ├── 001_Pound.json
│   │   ├── 002_KarateChop.json
│   │   └── ... (354 files)
│   ├── Maps/
│   │   ├── Route102/
│   │   │   ├── map.json
│   │   │   ├── scripts.csx
│   │   │   └── layout.tmx
│   │   └── ... (432 maps)
│   └── Graphics/
│       └── manifest.json
└── Sprites/
    └── (Copied from pokeemerald/graphics/)
```

## Usage Example

```csharp
// Command-line tool usage
dotnet run --project PokeSharp.Tools.Converter -- \
  --source /path/to/pokeemerald \
  --target /path/to/PokeSharp.Game/Assets \
  --convert all

// Programmatic usage
var pipeline = new ConversionPipeline();
pipeline.Initialize(pokeemeraldPath, pokeSharpPath);
var report = pipeline.ConvertAll();
```

## Data Validation

All converted data is validated:

✅ **Pokemon Species:**
- Base stat totals reasonable (200-780)
- At least one type specified
- Valid abilities
- EV yields sum ≤ 3
- Gender ratios valid

✅ **Moves:**
- Power 0-250
- Accuracy 0-100 or null
- PP 1-40
- Priority -7 to +5

✅ **Maps:**
- Valid connections
- Valid warp destinations
- Script files exist

## Performance Expectations

Based on pokeemerald data size:

| Data Type | Count | Est. Time |
|-----------|-------|-----------|
| Pokemon | 386 | ~2-3 seconds |
| Moves | 354 | ~1 second |
| Maps | 432 | ~10-15 seconds |
| Scripts | ~1,200 | ~30-60 seconds |
| **Total** | **~2,400** | **~1-2 minutes** |

## Error Handling

The conversion process includes:

1. **Validation errors** - Block conversion (e.g., missing required field)
2. **Conversion warnings** - Allow conversion but flag for review
3. **Detailed error reports** - Specify which item failed and why
4. **Partial success** - Convert what can be converted, report failures

## Next Steps

1. Review the design files in this directory
2. Implement `CStructParser` for parsing C code
3. Build `ConstantResolver` from pokeemerald constants
4. Create individual converter implementations
5. Build pipeline orchestrator
6. Test with sample data
7. Run full conversion
8. Validate results in PokeSharp game

## Questions?

This design is ready for implementation. Key files to start with:
- **PokeemeraldConverterDesign.cs** - Copy/adapt interfaces and classes
- **ImplementationGuide.md** - Follow implementation steps
- **ExampleOutputSchemas.md** - Reference for expected output

The design provides:
- ✅ Complete interface definitions
- ✅ Data structure mappings
- ✅ Conversion strategies
- ✅ Validation rules
- ✅ Example outputs
- ✅ Implementation roadmap

Ready to code!
