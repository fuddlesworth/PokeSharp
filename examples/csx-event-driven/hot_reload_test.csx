// hot_reload_test.csx - Automated hot-reload validation script
// Run this to verify hot-reload functionality is working correctly
using System.Diagnostics;

/// <summary>
/// Automated test script for validating hot-reload functionality
/// with event-driven CSX scripts.
/// </summary>
public class HotReloadTest : TypeScriptBase {

    public override void Execute(ScriptContext ctx) {
        ctx.Log.Info("═══════════════════════════════════════");
        ctx.Log.Info("  Hot-Reload Test Suite");
        ctx.Log.Info("═══════════════════════════════════════");

        var allPassed = true;

        // Test 1: Script Reload Performance
        allPassed &= TestReloadPerformance(ctx);

        // Test 2: Event Handler Registration
        allPassed &= TestEventHandlerRegistration(ctx);

        // Test 3: Memory Management
        allPassed &= TestMemoryManagement(ctx);

        // Test 4: Cross-Script Reload
        allPassed &= TestCrossScriptReload(ctx);

        // Final Report
        ctx.Log.Info("═══════════════════════════════════════");
        if (allPassed) {
            ctx.Log.Info("✅ All hot-reload tests PASSED");
        } else {
            ctx.Log.Error("❌ Some hot-reload tests FAILED");
        }
        ctx.Log.Info("═══════════════════════════════════════");
    }

    private bool TestReloadPerformance(ScriptContext ctx) {
        ctx.Log.Info("\n[Test 1] Script Reload Performance");
        ctx.Log.Info("───────────────────────────────────────");

        var scripts = new[] { "ice_tile.csx", "tall_grass.csx", "warp_tile.csx", "ledge.csx" };
        var watch = Stopwatch.StartNew();
        var allSucceeded = true;

        foreach (var script in scripts) {
            watch.Restart();
            var result = ctx.Scripting.ReloadScript(script);
            var elapsedMs = watch.ElapsedMilliseconds;

            if (result.Success && elapsedMs < 100) {
                ctx.Log.Info($"  ✅ {script}: {elapsedMs}ms (PASS)");
            } else {
                ctx.Log.Error($"  ❌ {script}: {elapsedMs}ms (FAIL - {result.ErrorMessage})");
                allSucceeded = false;
            }
        }

        if (allSucceeded) {
            ctx.Log.Info("  Result: All scripts reload in < 100ms");
            return true;
        } else {
            ctx.Log.Error("  Result: Some scripts failed or too slow");
            return false;
        }
    }

    private bool TestEventHandlerRegistration(ScriptContext ctx) {
        ctx.Log.Info("\n[Test 2] Event Handler Registration");
        ctx.Log.Info("───────────────────────────────────────");

        // Get baseline handler count
        var initialCount = ctx.EventSystem.GetHandlerCount();
        ctx.Log.Info($"  Initial handler count: {initialCount}");

        // Reload a script
        var result = ctx.Scripting.ReloadScript("ice_tile.csx");
        if (!result.Success) {
            ctx.Log.Error($"  ❌ Failed to reload: {result.ErrorMessage}");
            return false;
        }

        // Check handler count after reload
        var afterReloadCount = ctx.EventSystem.GetHandlerCount();
        ctx.Log.Info($"  After reload count: {afterReloadCount}");

        // Handler count should be the same (old removed, new added)
        if (afterReloadCount == initialCount) {
            ctx.Log.Info("  ✅ Handler count stable (no accumulation)");
            return true;
        } else {
            ctx.Log.Error($"  ❌ Handler count changed by {afterReloadCount - initialCount}");
            return false;
        }
    }

    private bool TestMemoryManagement(ScriptContext ctx) {
        ctx.Log.Info("\n[Test 3] Memory Management");
        ctx.Log.Info("───────────────────────────────────────");

        // Force GC and get baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var initialMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        ctx.Log.Info($"  Initial memory: {initialMemoryMB:F2} MB");

        // Perform multiple reloads
        const int reloadCount = 10;
        for (int i = 0; i < reloadCount; i++) {
            ctx.Scripting.ReloadScript("ice_tile.csx");
        }

        // Force GC again
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        var increaseMB = finalMemoryMB - initialMemoryMB;

        ctx.Log.Info($"  After {reloadCount} reloads: {finalMemoryMB:F2} MB");
        ctx.Log.Info($"  Increase: {increaseMB:F2} MB");

        // Allow up to 1MB increase per 10 reloads
        if (increaseMB < 1.0) {
            ctx.Log.Info("  ✅ Memory increase acceptable (< 1MB)");
            return true;
        } else {
            ctx.Log.Error($"  ❌ Memory increase too high ({increaseMB:F2} MB)");
            return false;
        }
    }

    private bool TestCrossScriptReload(ScriptContext ctx) {
        ctx.Log.Info("\n[Test 4] Cross-Script Reload");
        ctx.Log.Info("───────────────────────────────────────");

        var scripts = new[] { "ice_tile.csx", "tall_grass.csx" };
        var watch = Stopwatch.StartNew();

        // Reload both scripts rapidly
        foreach (var script in scripts) {
            var result = ctx.Scripting.ReloadScript(script);
            if (!result.Success) {
                ctx.Log.Error($"  ❌ Failed to reload {script}: {result.ErrorMessage}");
                return false;
            }
        }

        var totalTime = watch.ElapsedMilliseconds;
        ctx.Log.Info($"  Both scripts reloaded in {totalTime}ms");

        // Verify both are functional
        var iceLoaded = ctx.Scripting.IsScriptLoaded("ice_tile.csx");
        var grassLoaded = ctx.Scripting.IsScriptLoaded("tall_grass.csx");

        if (iceLoaded && grassLoaded) {
            ctx.Log.Info("  ✅ Both scripts operational after reload");
            return true;
        } else {
            ctx.Log.Error("  ❌ One or more scripts not loaded");
            return false;
        }
    }
}

return new HotReloadTest();
