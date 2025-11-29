# Porycon Per-Tileset Generation Refactoring

## Overview

This directory contains comprehensive analysis and planning documents for refactoring the porycon converter to generate **complete, reusable tilesets** instead of per-map tilesets.

## Problem Statement

**Current Behavior (WRONG):**
- Creates tileset FOR EACH MAP
- Only includes tiles USED by that specific map
- Results in ~300 duplicate tileset files (150 maps × 2 tilesets)
- Maps cannot be edited freely in Tiled (missing tiles)

**Desired Behavior (CORRECT):**
- Generate ONE tileset per tileset NAME (General, Mauville, etc.)
- Include ALL tiles from the tileset definition
- Results in ~20 shared tileset files
- Full editing capability in Tiled

## Documentation Files

### 1. porycon-refactoring-analysis.md (18KB)
**Comprehensive code quality analysis report**

Contains:
- Exact line numbers of problematic code
- Current vs desired architecture
- Data flow analysis
- Code smell detection
- Refactoring plan with time estimates (11-17 hours)
- Testing strategy
- Risk assessment

**Start here for**: Understanding WHY the refactoring is needed

---

### 2. porycon-data-flow-comparison.md (26KB)
**Visual comparison of current vs desired data flow**

Contains:
- ASCII diagrams showing current (broken) architecture
- ASCII diagrams showing desired (correct) architecture
- Step-by-step flow comparison
- Performance impact analysis
- Migration path visualization

**Start here for**: Understanding WHAT needs to change

---

### 3. porycon-refactoring-checklist.md (16KB)
**Practical implementation guide**

Contains:
- Exact code to change (copy-paste ready)
- Line-by-line refactoring instructions
- Step-by-step implementation order
- Testing checklist
- Rollback plan
- Success criteria

**Start here for**: Actually DOING the refactoring

---

## Quick Start Guide

### For Understanding the Problem:
1. Read **porycon-refactoring-analysis.md** Section: "Current Architecture Problems"
2. Look at **porycon-data-flow-comparison.md** diagrams
3. Review exact problem locations in analysis.md

### For Implementing the Fix:
1. Create feature branch: `git checkout -b feature/per-tileset-gen`
2. Follow **porycon-refactoring-checklist.md** step-by-step
3. Test after each step
4. Validate against success criteria

### For Code Review:
1. Check **porycon-refactoring-analysis.md** Section: "Required Changes"
2. Verify each file change against checklist
3. Run validation tests from checklist

---

## Key Findings Summary

### Critical Issues Found

1. **Tile Collection (converter.py:217-403)**
   - Tracks only USED tiles per map
   - Should track ALL tiles from tileset

2. **Tileset Building (__main__.py:272-337)**
   - Builds tilesets AFTER map conversion
   - Should build BEFORE, with complete tile sets

3. **Per-Map Tilesets (converter.py:1168-1317)**
   - Creates unique tileset per map
   - Should reference shared tileset files

### Files to Modify

1. **converter.py** (~250 lines removed, ~50 added)
   - Remove tile tracking
   - Remove `_create_tileset_for_map()`
   - Update tileset references

2. **__main__.py** (~80 lines removed, ~20 added)
   - Add tileset building before maps
   - Remove old tileset building code

3. **tileset_builder.py** (~200-300 lines modified)
   - Replace `used_tiles` with `tilesets`
   - Add `load_complete_tileset()`
   - Update `build_tileset_image()`

4. **metatile_loader.py** (NEW, ~150 lines)
   - Create `load_complete_metatiles()`

---

## Implementation Roadmap

### Phase 1: Infrastructure (2-3 hours)
- Create `metatile_loader.py`
- Implement `load_complete_metatiles()`
- Test on General tileset

### Phase 2: TilesetBuilder Refactor (2-3 hours)
- Update data structures
- Add `load_complete_tileset()`
- Update `build_tileset_image()`

### Phase 3: Main Flow Update (1-2 hours)
- Move tileset building before maps
- Remove old building code

### Phase 4: Converter Cleanup (2-3 hours)
- Remove tile tracking
- Update tileset references
- Remove `_create_tileset_for_map()`

### Phase 5: Validation (2-3 hours)
- Unit tests
- Integration tests
- Visual validation in Tiled

**Total Estimate**: 10-15 hours

---

## Expected Outcomes

### Before Refactoring:
```
Output/
├─ Tilesets/
│  ├─ route101_general.png       (8 tiles, incomplete)
│  ├─ route102_general.png       (7 tiles, incomplete)
│  ├─ route103_general.png       (12 tiles, incomplete)
│  └─ ... (300 files total)
└─ Data/Maps/
   └─ ... (150 map files)
```

### After Refactoring:
```
Output/
├─ Tilesets/hoenn/
│  ├─ general.json               (complete, ~8000 tiles)
│  ├─ general.png
│  ├─ mauville.json              (complete, ~8000 tiles)
│  ├─ mauville.png
│  └─ ... (~20 files total)
├─ Sprites/TileAnimations/
│  ├─ General/water_anim/
│  └─ Mauville/fountain_anim/
└─ Data/Maps/hoenn/
   ├─ route_101.json             (refs ../../Tilesets/hoenn/general.json)
   ├─ route_102.json             (refs ../../Tilesets/hoenn/general.json)
   └─ route_103.json             (refs ../../Tilesets/hoenn/general.json)
```

### Improvements:
- ✓ 300 files → 20 files (93% reduction)
- ✓ Complete tilesets (all tiles available)
- ✓ Full editing in Tiled
- ✓ 40-60% smaller file size
- ✓ 20-30% faster conversion

---

## Testing Strategy

### Unit Tests Required:
1. `test_load_complete_metatiles()` - Loads all metatiles
2. `test_build_complete_tileset()` - Includes all tiles
3. `test_tileset_deduplication()` - One file per tileset

### Integration Tests Required:
1. Convert 5 maps, verify same tileset referenced
2. Check Sprites/TileAnimations exported once
3. Verify tile count matches source

### Manual Validation:
1. Open map in Tiled - should render correctly
2. All tiles available for editing
3. Compare file sizes (should be smaller)

---

## Risk Assessment

### High Risk Areas:
- Used tiles logic is deeply embedded
- Animation timing must be perfect
- firstgid calculations must remain correct

### Mitigation:
- Create feature branch (don't break main)
- Test after each step
- Keep rollback option available
- Comprehensive validation before merge

---

## Success Criteria

- [ ] Tilesets built exactly once (before maps)
- [ ] ~20 tileset files created (not 300)
- [ ] Each tileset contains ALL tiles (~8000)
- [ ] Maps reference shared tileset files
- [ ] Sprites/TileAnimations exported once per tileset
- [ ] Maps load correctly in Tiled
- [ ] All tiles available for editing
- [ ] File size reduced by 40-60%
- [ ] Conversion speed improved by 20-30%

---

## Related Issues

This refactoring addresses:
- Tileset incompleteness
- File duplication
- Limited Tiled editing capability
- Incorrect tileset reusability model

---

## Additional Resources

### External Links:
- Tiled Documentation: https://doc.mapeditor.org/en/stable/
- pokeemerald Structure: https://github.com/pret/pokeemerald

### Internal Files:
- `porycon/porycon/converter.py` - Main conversion logic
- `porycon/porycon/__main__.py` - Entry point
- `porycon/porycon/tileset_builder.py` - Tileset building
- `porycon/porycon/metatile.py` - Metatile parsing

---

## Version History

**v1.0** (2025-11-28)
- Initial analysis and refactoring plan
- Three comprehensive documentation files
- Ready for implementation

---

## Contact / Questions

For questions about this refactoring:
1. Review the three documentation files in order
2. Check the specific section related to your question
3. All code locations include exact line numbers

---

## License

Same as parent PokeSharp project.
