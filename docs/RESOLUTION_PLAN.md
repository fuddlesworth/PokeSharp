# Entity Framework Data Loading: Resolution Plan

**Status:** CRITICAL - NPCs and Trainers DbSets empty
**Root Cause:** Missing JSON data files and directories
**Resolution Time:** 30 minutes to 2 hours (depending on data source)

---

## Phase 1: Investigation (5-10 minutes)

### Step 1.1: Check Git History
```bash
# Look for when NPCs/Trainers directories were removed or never added
git log --all --oneline -- "Assets/Definitions/NPCs/" | head -20
git log --all --oneline -- "Assets/Definitions/Trainers/" | head -20

# Check all branches for these directories
git branch -a | xargs -I {} git ls-tree -r {} -- "Assets/Definitions/NPCs/" 2>/dev/null | grep -q . && echo "Found in {}"
```

### Step 1.2: Check .gitignore
```bash
# See if these paths are excluded
grep -n "NPCs\|Trainers" .gitignore

# Look for patterns that might exclude them
grep -n "Assets\|Definitions" .gitignore
```

### Step 1.3: Check External Tools
Look for any scripts or configs that generate NPC/Trainer data:
- porycon configuration files
- ROM extraction scripts
- Data generation tools
- Automation workflows

### Step 1.4: Search Documentation
```bash
# Look for references in docs/comments
grep -r "NPC.*json\|Trainer.*json" docs/ --include="*.md"
grep -r "data.*generation\|extract.*data" . --include="*.md" --include="*.txt"
```

---

## Phase 2: Data Source Determination (10-20 minutes)

### Scenario A: Data Exists in Git History
```bash
# Restore from history
git checkout <commit-hash> -- Assets/Definitions/NPCs/
git checkout <commit-hash> -- Assets/Definitions/Trainers/

# Verify restoration
ls -la Assets/Definitions/NPCs/ | head
ls -la Assets/Definitions/Trainers/ | head

# Count files
find Assets/Definitions/NPCs -name "*.json" | wc -l
find Assets/Definitions/Trainers -name "*.json" | wc -l
```

### Scenario B: Data Exists in Another Branch
```bash
# List all branches and their NPC/Trainer directories
for branch in $(git branch -a); do
  echo "Branch: $branch"
  git ls-tree -r $branch -- "Assets/Definitions/NPCs" 2>/dev/null | wc -l
done

# If found, merge or cherry-pick
git cherry-pick <commit-hash>
```

### Scenario C: Data Generated Externally
Check for:
1. **porycon** - Pokemon game ROM extraction tool
   ```bash
   # Look for porycon config
   find . -name "*porycon*" -o -name "*.porycon"

   # Check if there's a build script
   cat package.json | grep porycon
   ```

2. **ROM extraction scripts**
   ```bash
   # Look for Python/Bash scripts
   find scripts/ -name "*.py" -o -name "*.sh" | xargs grep -l "extract\|npc\|trainer"
   ```

3. **Data files in other formats**
   ```bash
   # CSV, XLSX, custom formats
   find . -name "*npc*" -o -name "*trainer*" | grep -v node_modules | grep -v ".git"
   ```

### Scenario D: Feature Not Yet Implemented
If no data source found:
- Confirm with team that this is a stub feature
- Create empty directories (data will be added later)
- Proceed with placeholder data for testing

---

## Phase 3: Create Missing Directories (1 minute)

```bash
# Create the directories if they don't exist
mkdir -p Assets/Definitions/NPCs/
mkdir -p Assets/Definitions/Trainers/

# Verify
ls -la Assets/Definitions/ | grep -E "NPCs|Trainers"
```

---

## Phase 4: Populate with Data (10-30 minutes)

### Option 1: Restore Existing Data
```bash
# If data was found in git history or another branch
# Files will be automatically in place

# Verify counts
echo "NPCs: $(find Assets/Definitions/NPCs -name "*.json" | wc -l)"
echo "Trainers: $(find Assets/Definitions/Trainers -name "*.json" | wc -l)"
```

### Option 2: Create Starter Data
If no data source found, create minimal examples:

**File: Assets/Definitions/NPCs/starter_npc.json**
```json
{
  "npcId": "starter_npc_001",
  "displayName": "Professor Oak",
  "npcType": "scientist",
  "spriteId": "scientist/professor_oak",
  "behaviorScript": "oak_behavior",
  "dialogueScript": "oak_greeting",
  "movementSpeed": 3.75,
  "customProperties": {
    "location": "lab",
    "role": "guide"
  },
  "sourceMod": "base",
  "version": "1.0.0"
}
```

**File: Assets/Definitions/Trainers/starter_trainer.json**
```json
{
  "trainerId": "starter_trainer_001",
  "displayName": "Youngster Joey",
  "trainerClass": "youngster",
  "spriteId": "youngster/joey",
  "prizeMoney": 100,
  "items": ["potion"],
  "aiScript": "basic_ai",
  "introDialogue": "Hey! You!",
  "defeatDialogue": "Aw, man...",
  "isRematchable": false,
  "party": [
    {
      "species": "rattata",
      "level": 5,
      "moves": ["tackle", "quick_attack"]
    }
  ],
  "customProperties": {
    "location": "route_1"
  },
  "sourceMod": "base",
  "version": "1.0.0"
}
```

---

## Phase 5: Verify Loading (5 minutes)

### Step 5.1: Run the Game
```bash
dotnet run --configuration Debug
```

### Step 5.2: Check Logs
Look for in console/debug output:
```
[INF] A ✓ Game data loader initialized | database: Assets/Definitions
[INF] Game data definitions loaded successfully
[DBG] Loaded NPC: starter_npc_001
[DBG] Loaded Trainer: starter_trainer_001
```

### Step 5.3: Open EntityFrameworkPanel
In-game, open the EntityFrameworkPanel:
- Verify **NPCs** count > 0
- Verify **Trainers** count > 0
- Verify all other counts unchanged

### Step 5.4: Manual Verification
Add temporary debug code to verify:

```csharp
// In LoadGameDataStep.cs or equivalent
var npcCount = context.Npcs.Count();
var trainerCount = context.Trainers.Count();

logger.LogInformation("NPC count after load: {Count}", npcCount);
logger.LogInformation("Trainer count after load: {Count}", trainerCount);

if (npcCount == 0 || trainerCount == 0)
{
    logger.LogWarning("WARNING: No NPCs or Trainers loaded!");
}
```

---

## Phase 6: Test Coverage (30 minutes)

### Step 6.1: Create Test Project
```bash
# Create xUnit test project
dotnet new xunit -n MonoBallFramework.Game.Tests -o MonoBallFramework.Game.Tests

# Add references
cd MonoBallFramework.Game.Tests
dotnet add reference ../MonoBallFramework.Game/MonoBallFramework.Game.csproj
dotnet add package Moq 4.16.0
dotnet add package Microsoft.EntityFrameworkCore.InMemory
```

### Step 6.2: Implement Core Tests
Create `GameDataLoaderTests.cs`:

```csharp
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using MonoBallFramework.Game.GameData.Loading;
using MonoBallFramework.Game.GameData;

namespace MonoBallFramework.Game.Tests;

public class GameDataLoaderTests
{
    [Fact]
    public async Task LoadAllAsync_LoadsNpcs_WhenDirectoryExists()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<GameDataContext>()
            .UseInMemoryDatabase($"test_{Guid.NewGuid()}")
            .Options;

        using var context = new GameDataContext(options);
        var mockLogger = new Mock<ILogger<GameDataLoader>>();
        var loader = new GameDataLoader(context, mockLogger.Object);

        var dataPath = "Assets/Definitions";  // Assumes data exists

        // Act
        await loader.LoadAllAsync(dataPath);

        // Assert
        var npcCount = context.Npcs.Count();
        Assert.True(npcCount > 0, "NPCs should be loaded");
    }

    [Fact]
    public async Task LoadAllAsync_LoadsTrainers_WhenDirectoryExists()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<GameDataContext>()
            .UseInMemoryDatabase($"test_{Guid.NewGuid()}")
            .Options;

        using var context = new GameDataContext(options);
        var mockLogger = new Mock<ILogger<GameDataLoader>>();
        var loader = new GameDataLoader(context, mockLogger.Object);

        var dataPath = "Assets/Definitions";

        // Act
        await loader.LoadAllAsync(dataPath);

        // Assert
        var trainerCount = context.Trainers.Count();
        Assert.True(trainerCount > 0, "Trainers should be loaded");
    }

    [Fact]
    public async Task LoadAllAsync_LoadsAllDataTypes()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<GameDataContext>()
            .UseInMemoryDatabase($"test_{Guid.NewGuid()}")
            .Options;

        using var context = new GameDataContext(options);
        var mockLogger = new Mock<ILogger<GameDataLoader>>();
        var loader = new GameDataLoader(context, mockLogger.Object);

        var dataPath = "Assets/Definitions";

        // Act
        await loader.LoadAllAsync(dataPath);

        // Assert
        Assert.True(context.Npcs.Count() >= 1, "NPCs not loaded");
        Assert.True(context.Trainers.Count() >= 1, "Trainers not loaded");
        Assert.True(context.Maps.Count() > 0, "Maps not loaded");
        Assert.True(context.AudioDefinitions.Count() > 0, "Audio not loaded");
        Assert.True(context.PopupThemes.Count() > 0, "Themes not loaded");
        Assert.True(context.MapSections.Count() > 0, "Sections not loaded");
    }
}
```

### Step 6.3: Run Tests
```bash
dotnet test MonoBallFramework.Game.Tests

# Expected output:
# ✓ LoadAllAsync_LoadsNpcs_WhenDirectoryExists
# ✓ LoadAllAsync_LoadsTrainers_WhenDirectoryExists
# ✓ LoadAllAsync_LoadsAllDataTypes
```

---

## Phase 7: Documentation Update (5 minutes)

### Update README or SETUP.md
Add:
```markdown
## Data Loading

The game loads entity definitions from JSON files in `Assets/Definitions/`:

- **NPCs:** `Assets/Definitions/NPCs/*.json` (recursive)
- **Trainers:** `Assets/Definitions/Trainers/*.json` (recursive)
- **Maps:** `Assets/Definitions/Maps/Regions/*.json` (recursive)
- **Audio:** `Assets/Definitions/Audio/**/*.json` (recursive)
- **Popups:** `Assets/Definitions/Maps/Popups/Themes/*.json`
- **Sections:** `Assets/Definitions/Maps/Sections/*.json`

All data is loaded into an in-memory EF Core database during initialization.
Total load time: < 3 seconds for 1,100+ definitions.
```

---

## Phase 8: Validation Checklist

- [ ] NPCs directory exists: `Assets/Definitions/NPCs/`
- [ ] Trainers directory exists: `Assets/Definitions/Trainers/`
- [ ] At least 1 NPC JSON file in NPCs directory
- [ ] At least 1 Trainer JSON file in Trainers directory
- [ ] JSON files have valid syntax (not malformed)
- [ ] NPCs have required fields: `npcId`
- [ ] Trainers have required fields: `trainerId`
- [ ] Game initializes without errors
- [ ] EntityFrameworkPanel shows > 0 for both NPCs and Trainers
- [ ] Unit tests pass (at least 3 tests)
- [ ] Integration tests pass
- [ ] Performance is acceptable (< 3 seconds total)

---

## Rollback Plan

If anything breaks:

```bash
# Revert changes
git checkout -- Assets/Definitions/

# Or specific directories
git checkout -- Assets/Definitions/NPCs/
git checkout -- Assets/Definitions/Trainers/

# Verify game still runs with original state
dotnet run
```

---

## Success Criteria

**All of the following must be true:**

1. ✓ `Assets/Definitions/NPCs/` directory exists and contains JSON files
2. ✓ `Assets/Definitions/Trainers/` directory exists and contains JSON files
3. ✓ GameDataLoader successfully loads both data types (no errors in logs)
4. ✓ context.Npcs.Count() > 0
5. ✓ context.Trainers.Count() > 0
6. ✓ EntityFrameworkPanel displays correct counts
7. ✓ Unit tests pass
8. ✓ Game runs without crashes

---

## Time Estimate

| Phase | Time | Notes |
|-------|------|-------|
| Investigation | 5-10 min | Depends on whether data exists |
| Determination | 10-20 min | May find data in history/branches |
| Create Directories | 1 min | Quick |
| Populate Data | 10-30 min | May be instant if restoring, longer if creating new |
| Verify Loading | 5 min | Quick test run |
| Test Coverage | 30 min | Optional but recommended |
| Documentation | 5 min | Quick |
| **TOTAL** | **60-95 minutes** | **Most likely 30-45 min** |

---

## Contact/Questions

If you encounter issues during resolution:

1. Check the logs - they'll tell you exactly which file/directory is missing
2. Review the JSON schema in `data_loading_reference.md`
3. Run the test suite - it validates each component
4. Check git history - data may have existed previously

---

**Document Status:** Ready for execution
**Prepared by:** QA/Tester Agent
**Last Updated:** 2025-12-12
**Estimated Completion:** 30-95 minutes from start
