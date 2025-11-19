# Debug Console - Scrolling Guide

The debug console includes full scrolling support to navigate through large amounts of output.

## Scrolling Controls

### Keyboard Controls

| Key | Action |
|-----|--------|
| **PageUp** | Scroll up one page (full screen of lines) |
| **PageDown** | Scroll down one page (full screen of lines) |
| **Ctrl+Home** | Jump to the top of output |
| **Ctrl+End** | Jump to the bottom of output |

### Visual Indicators

The console displays a **scrollbar** on the right side when there's more content than can fit on screen:

- **Scrollbar Track** - Dark gray background shows the full content range
- **Scrollbar Thumb** - Light gray indicator shows your current position
  - **Blue** when at the top or bottom
  - **Gray** when in the middle
- **Arrow Indicators**:
  - **`^` (yellow)** appears above scrollbar when more content is above
  - **`v` (yellow)** appears below scrollbar when more content is below

## Scrolling Behavior

### Auto-Scroll

The console automatically scrolls to the bottom when:
- New output is added
- You execute a command
- The console is opened

### Manual Scrolling

Once you manually scroll up:
- The console stays at that position
- New output still gets added to the bottom
- You can continue reading older content

To get back to the latest output:
- Press **`Ctrl+End`** to jump to bottom
- Press **`PageDown`** repeatedly
- Execute any command (auto-scrolls after execution)

## Example Usage

### Viewing Long Output

```csharp
// Generate lots of output
for (int i = 0; i < 100; i++)
{
    Print($"Line {i}");
}

// Use PageUp to scroll back and see early lines
// Use PageDown to scroll forward
// Use Ctrl+Home to jump to "Line 0"
// Use Ctrl+End to jump to "Line 99"
```

### Reviewing Command History

```csharp
// Execute several commands
CountEntities()
ListEntities()
Help()

// Scroll up with PageUp to review the output
// Each command's output is color-coded:
//   - Cyan: Your input
//   - Light Green: Success output
//   - Red: Errors
```

### Debugging Long Scripts

```csharp
// Run a script that generates lots of output
var entities = GetAllEntities();
foreach (var entity in entities)
{
    Inspect(entity);
}

// Scroll through results with PageUp/PageDown
// Jump to top with Ctrl+Home to see first entity
// Jump to bottom with Ctrl+End to see last entity
```

## Tips & Tricks

### Quick Navigation

```csharp
// To quickly find something in output:
// 1. Generate output
ListEntities()

// 2. Jump to top
//    Press: Ctrl+Home

// 3. Read through with PageDown
//    Press: PageDown (multiple times)

// 4. Jump back to bottom
//    Press: Ctrl+End
```

### Reading While Commands Execute

```csharp
// You can scroll up to read old output
// while new output is being generated

// The scrollbar thumb will shrink as more content is added
// Yellow arrows will indicate there's new content below
```

### Buffer Size

The console keeps the last **1000 lines** in the buffer:
- Older lines are automatically removed
- This prevents memory issues with very long sessions
- If you need to keep output, copy it before it scrolls off

## Scrollbar Visual Reference

```
Console Output Area
┌─────────────────────────────────┬──┐
│ > CountEntities()               │^│  <- More content above
│ 42                              │││
│ > ListEntities()                │││
│ Total entities: 42              │││
│   Entity 0: 5 component(s)      │││
│   Entity 1: 3 component(s)      │██│  <- Scrollbar thumb (blue = at edge)
│   Entity 2: 4 component(s)      │██│
│   ...                           │││
│   Entity 20: 2 component(s)     │││
│                                 │v│  <- More content below
├─────────────────────────────────┴──┤
│ > _                              │  <- Input area
└──────────────────────────────────┘
```

## Troubleshooting

### Scrollbar Not Appearing
- **Cause**: Not enough output to scroll
- **Solution**: Generate more output (e.g., `ListEntities()` or `Help()`)

### Can't Scroll Up
- **Cause**: Already at the top of output
- **Solution**: Check if the scrollbar thumb is at the very top (blue color)

### Output Jumps to Bottom
- **Cause**: New output was added (auto-scroll behavior)
- **Solution**: This is normal. Scroll back up to continue reading

### Lost My Place While Scrolling
- **Cause**: Executed a command (auto-scrolls to bottom)
- **Solution**: Press **`Ctrl+Home`** to start from the top again

## Keyboard Shortcuts Summary

```
Scrolling:
  PageUp      ↑  Scroll up one page
  PageDown    ↓  Scroll down one page
  Ctrl+Home   ⇑  Jump to top
  Ctrl+End    ⇓  Jump to bottom

History:
  Up Arrow    ↑  Previous command
  Down Arrow  ↓  Next command

Console:
  ` or ~      Toggle console
  Enter       Execute command
  Escape      Close console
```

## See Also

- [Console Examples](CONSOLE_EXAMPLES.md) - Command examples
- [Console API Guide](CONSOLE_API_GUIDE.md) - Roslyn API reference
- [Console Troubleshooting](CONSOLE_TROUBLESHOOTING.md) - Common issues

