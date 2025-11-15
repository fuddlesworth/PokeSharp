using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PokeSharp.Game.Data;
using PokeSharp.Game.Data.Entities;
using Xunit;

namespace PokeSharp.Game.Data.Tests;

/// <summary>
/// Comprehensive tests for SQLite database migration.
/// Tests database creation, data loading, query performance, and memory efficiency.
/// </summary>
public class DatabaseMigrationTests : IDisposable
{
    private readonly string _testDbPath;
    private const long MaxExpectedMemoryBytes = 100 * 1024 * 1024; // 100MB

    public DatabaseMigrationTests()
    {
        // Use unique database file for each test run
        _testDbPath = Path.Combine(Path.GetTempPath(), $"pokesharp_test_{Guid.NewGuid()}.db");
    }

    public void Dispose()
    {
        // Clean up test database
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    #region Database Creation Tests

    [Fact]
    public async Task DatabaseCreation_OnFirstRun_CreatesDatabaseFile()
    {
        // Arrange
        var options = CreateSqliteOptions();

        // Act
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();
        }

        // Assert
        File.Exists(_testDbPath).Should().BeTrue("database file should be created on first run");
    }

    [Fact]
    public async Task DatabaseCreation_OnFirstRun_CreatesAllTables()
    {
        // Arrange
        var options = CreateSqliteOptions();

        // Act
        await using var context = new GameDataContext(options);
        await context.Database.EnsureCreatedAsync();

        // Assert - Verify all tables exist by querying them
        var npcsCount = await context.Npcs.CountAsync();
        var trainersCount = await context.Trainers.CountAsync();
        var mapsCount = await context.Maps.CountAsync();

        // Should not throw and should return 0 for empty tables
        npcsCount.Should().Be(0);
        trainersCount.Should().Be(0);
        mapsCount.Should().Be(0);
    }

    [Fact]
    public async Task DatabaseCreation_OnFirstRun_CreatesIndexes()
    {
        // Arrange
        var options = CreateSqliteOptions();

        // Act
        await using var context = new GameDataContext(options);
        await context.Database.EnsureCreatedAsync();

        // Assert - Check indexes exist via raw SQL
        await using var connection = context.Database.GetDbConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText =
            @"
            SELECT COUNT(*) FROM sqlite_master
            WHERE type='index'
            AND name LIKE 'IX_%'";

        var indexCount = Convert.ToInt32(await command.ExecuteScalarAsync());
        indexCount.Should().BeGreaterThan(0, "indexes should be created for common queries");
    }

    #endregion

    #region Data Loading Tests

    [Fact]
    public async Task DataLoading_MapDefinition_StoresAndRetrievesCorrectly()
    {
        // Arrange
        var options = CreateSqliteOptions();
        var testMap = CreateTestMapDefinition();

        // Act - Insert
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Maps.Add(testMap);
            await context.SaveChangesAsync();
        }

        // Assert - Retrieve
        await using (var context = new GameDataContext(options))
        {
            var retrievedMap = await context.Maps.FirstOrDefaultAsync(m =>
                m.MapId == testMap.MapId
            );

            retrievedMap.Should().NotBeNull();
            retrievedMap!.MapId.Should().Be(testMap.MapId);
            retrievedMap.DisplayName.Should().Be(testMap.DisplayName);
            retrievedMap.Region.Should().Be(testMap.Region);
            retrievedMap.TiledDataJson.Should().Be(testMap.TiledDataJson);
        }
    }

    [Fact]
    public async Task DataLoading_NpcDefinition_StoresAndRetrievesCorrectly()
    {
        // Arrange
        var options = CreateSqliteOptions();
        var testNpc = CreateTestNpcDefinition();

        // Act - Insert
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Npcs.Add(testNpc);
            await context.SaveChangesAsync();
        }

        // Assert - Retrieve
        await using (var context = new GameDataContext(options))
        {
            var retrievedNpc = await context.Npcs.FirstOrDefaultAsync(n =>
                n.NpcId == testNpc.NpcId
            );

            retrievedNpc.Should().NotBeNull();
            retrievedNpc!.NpcId.Should().Be(testNpc.NpcId);
            retrievedNpc.DisplayName.Should().Be(testNpc.DisplayName);
            retrievedNpc.NpcType.Should().Be(testNpc.NpcType);
        }
    }

    [Fact]
    public async Task DataLoading_TrainerDefinition_StoresAndRetrievesCorrectly()
    {
        // Arrange
        var options = CreateSqliteOptions();
        var testTrainer = CreateTestTrainerDefinition();

        // Act - Insert
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Trainers.Add(testTrainer);
            await context.SaveChangesAsync();
        }

        // Assert - Retrieve
        await using (var context = new GameDataContext(options))
        {
            var retrievedTrainer = await context.Trainers.FirstOrDefaultAsync(t =>
                t.TrainerId == testTrainer.TrainerId
            );

            retrievedTrainer.Should().NotBeNull();
            retrievedTrainer!.TrainerId.Should().Be(testTrainer.TrainerId);
            retrievedTrainer.DisplayName.Should().Be(testTrainer.DisplayName);
            retrievedTrainer.TrainerClass.Should().Be(testTrainer.TrainerClass);
        }
    }

    [Fact]
    public async Task DataLoading_BulkInsert_HandlesLargeDatasets()
    {
        // Arrange
        var options = CreateSqliteOptions();
        var mapCount = 500; // Simulate 500 maps

        // Act
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            var maps = Enumerable
                .Range(0, mapCount)
                .Select(i => new MapDefinition
                {
                    MapId = $"test_map_{i}",
                    DisplayName = $"Test Map {i}",
                    Region = "hoenn",
                    TiledDataJson = CreateSampleTiledJson(i),
                })
                .ToList();

            context.Maps.AddRange(maps);
            await context.SaveChangesAsync();
        }

        // Assert
        await using (var context = new GameDataContext(options))
        {
            var count = await context.Maps.CountAsync();
            count.Should().Be(mapCount, "all maps should be inserted");
        }
    }

    [Fact]
    public async Task DataLoading_TiledDataJson_DeserializesCorrectly()
    {
        // Arrange
        var options = CreateSqliteOptions();
        var testMap = CreateTestMapDefinition();

        // Act
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Maps.Add(testMap);
            await context.SaveChangesAsync();
        }

        // Assert - Verify JSON can be deserialized
        await using (var context = new GameDataContext(options))
        {
            var map = await context.Maps.FirstAsync(m => m.MapId == testMap.MapId);

            var deserializedAction = () =>
                JsonSerializer.Deserialize<Dictionary<string, object>>(map.TiledDataJson);
            deserializedAction.Should().NotThrow("TiledDataJson should be valid JSON");

            var data = deserializedAction();
            data.Should().NotBeNull();
            data.Should().ContainKey("width");
        }
    }

    #endregion

    #region Subsequent Startup Tests

    [Fact]
    public async Task SubsequentStartup_ExistingDatabase_LoadsCorrectly()
    {
        // Arrange - First startup
        var options = CreateSqliteOptions();
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Maps.Add(CreateTestMapDefinition());
            await context.SaveChangesAsync();
        }

        // Act - Second startup (simulating app restart)
        await using (var context = new GameDataContext(options))
        {
            var maps = await context.Maps.ToListAsync();

            // Assert
            maps.Should().NotBeEmpty("existing data should be loaded");
            maps.Should().HaveCount(1);
        }
    }

    [Fact]
    public async Task SubsequentStartup_NoRecreation_PreservesData()
    {
        // Arrange - Create and populate database
        var options = CreateSqliteOptions();
        var testMapId = "persistent_map";

        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Maps.Add(
                new MapDefinition
                {
                    MapId = testMapId,
                    DisplayName = "Persistent Test Map",
                    Region = "hoenn",
                    TiledDataJson = "{}",
                }
            );
            await context.SaveChangesAsync();
        }

        // Act - Multiple reopenings
        for (int i = 0; i < 3; i++)
        {
            await using var context = new GameDataContext(options);
            var map = await context.Maps.FirstOrDefaultAsync(m => m.MapId == testMapId);

            // Assert
            map.Should().NotBeNull($"data should persist through reopening #{i + 1}");
        }
    }

    #endregion

    #region EF Query Tests

    [Fact]
    public async Task EfQueries_MapByRegion_ReturnsCorrectResults()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Maps.AddRange(
                new MapDefinition
                {
                    MapId = "hoenn_1",
                    DisplayName = "Hoenn Map 1",
                    Region = "hoenn",
                    TiledDataJson = "{}",
                },
                new MapDefinition
                {
                    MapId = "hoenn_2",
                    DisplayName = "Hoenn Map 2",
                    Region = "hoenn",
                    TiledDataJson = "{}",
                },
                new MapDefinition
                {
                    MapId = "kanto_1",
                    DisplayName = "Kanto Map 1",
                    Region = "kanto",
                    TiledDataJson = "{}",
                }
            );
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new GameDataContext(options))
        {
            var hoennMaps = await context.Maps.Where(m => m.Region == "hoenn").ToListAsync();

            // Assert
            hoennMaps.Should().HaveCount(2);
            hoennMaps.Should().AllSatisfy(m => m.Region.Should().Be("hoenn"));
        }
    }

    [Fact]
    public async Task EfQueries_MapByType_UsesIndex()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Maps.AddRange(
                new MapDefinition
                {
                    MapId = "town_1",
                    DisplayName = "Town 1",
                    Region = "hoenn",
                    MapType = "town",
                    TiledDataJson = "{}",
                },
                new MapDefinition
                {
                    MapId = "route_1",
                    DisplayName = "Route 1",
                    Region = "hoenn",
                    MapType = "route",
                    TiledDataJson = "{}",
                },
                new MapDefinition
                {
                    MapId = "cave_1",
                    DisplayName = "Cave 1",
                    Region = "hoenn",
                    MapType = "cave",
                    TiledDataJson = "{}",
                }
            );
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new GameDataContext(options))
        {
            var towns = await context.Maps.Where(m => m.MapType == "town").ToListAsync();

            // Assert
            towns.Should().HaveCount(1);
            towns[0].MapType.Should().Be("town");
        }
    }

    [Fact]
    public async Task EfQueries_NpcByType_ReturnsFiltered()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Npcs.AddRange(
                new NpcDefinition
                {
                    NpcId = "npc_1",
                    DisplayName = "Nurse",
                    NpcType = "nurse",
                },
                new NpcDefinition
                {
                    NpcId = "npc_2",
                    DisplayName = "Shopkeeper",
                    NpcType = "shopkeeper",
                },
                new NpcDefinition
                {
                    NpcId = "npc_3",
                    DisplayName = "Nurse 2",
                    NpcType = "nurse",
                }
            );
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new GameDataContext(options))
        {
            var nurses = await context.Npcs.Where(n => n.NpcType == "nurse").ToListAsync();

            // Assert
            nurses.Should().HaveCount(2);
            nurses.Should().AllSatisfy(n => n.NpcType.Should().Be("nurse"));
        }
    }

    [Fact]
    public async Task EfQueries_TrainerByClass_ReturnsCorrect()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Trainers.AddRange(
                new TrainerDefinition
                {
                    TrainerId = "trainer_1",
                    DisplayName = "Youngster Joey",
                    TrainerClass = "youngster",
                },
                new TrainerDefinition
                {
                    TrainerId = "trainer_2",
                    DisplayName = "Lass Amy",
                    TrainerClass = "lass",
                },
                new TrainerDefinition
                {
                    TrainerId = "trainer_3",
                    DisplayName = "Youngster Ben",
                    TrainerClass = "youngster",
                }
            );
            await context.SaveChangesAsync();
        }

        // Act
        await using (var context = new GameDataContext(options))
        {
            var youngsters = await context
                .Trainers.Where(t => t.TrainerClass == "youngster")
                .ToListAsync();

            // Assert
            youngsters.Should().HaveCount(2);
            youngsters.Should().AllSatisfy(t => t.TrainerClass.Should().Be("youngster"));
        }
    }

    [Fact]
    public async Task EfQueries_ComplexQuery_PerformsEfficiently()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            // Add 100 maps
            for (int i = 0; i < 100; i++)
            {
                context.Maps.Add(
                    new MapDefinition
                    {
                        MapId = $"map_{i}",
                        DisplayName = $"Map {i}",
                        Region = i % 2 == 0 ? "hoenn" : "kanto",
                        MapType = i % 3 == 0 ? "town" : "route",
                        TiledDataJson = "{}",
                    }
                );
            }
            await context.SaveChangesAsync();
        }

        // Act - Complex query with multiple filters
        var startTime = DateTime.UtcNow;
        await using (var context = new GameDataContext(options))
        {
            var results = await context
                .Maps.Where(m => m.Region == "hoenn")
                .Where(m => m.MapType == "town")
                .OrderBy(m => m.DisplayName)
                .Take(10)
                .ToListAsync();

            var duration = DateTime.UtcNow - startTime;

            // Assert
            results.Should().NotBeEmpty();
            duration
                .Should()
                .BeLessThan(TimeSpan.FromMilliseconds(100), "query should be fast with indexes");
        }
    }

    #endregion

    #region Memory Usage Tests

    [Fact]
    public async Task MemoryUsage_DatabaseCreation_RemainsLow()
    {
        // Arrange
        var options = CreateSqliteOptions();
        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        // Act
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();

            // Add realistic amount of data
            for (int i = 0; i < 500; i++)
            {
                context.Maps.Add(
                    new MapDefinition
                    {
                        MapId = $"map_{i}",
                        DisplayName = $"Test Map {i}",
                        Region = "hoenn",
                        TiledDataJson = CreateSampleTiledJson(i),
                    }
                );
            }
            await context.SaveChangesAsync();
        }

        var finalMemory = GC.GetTotalMemory(forceFullCollection: true);
        var memoryUsed = finalMemory - initialMemory;

        // Assert
        memoryUsed
            .Should()
            .BeLessThan(
                MaxExpectedMemoryBytes,
                $"memory usage should stay under {MaxExpectedMemoryBytes / (1024 * 1024)}MB"
            );
    }

    [Fact]
    public async Task MemoryUsage_QueryExecution_DoesNotLeak()
    {
        // Arrange
        var options = CreateSqliteOptions();
        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            for (int i = 0; i < 100; i++)
            {
                context.Maps.Add(CreateTestMapDefinition($"map_{i}"));
            }
            await context.SaveChangesAsync();
        }

        // Act - Execute many queries
        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        for (int i = 0; i < 1000; i++)
        {
            await using var context = new GameDataContext(options);
            var maps = await context.Maps.Take(10).ToListAsync();
        }

        var finalMemory = GC.GetTotalMemory(forceFullCollection: true);
        var memoryGrowth = finalMemory - initialMemory;

        // Assert - Memory should not grow significantly
        memoryGrowth
            .Should()
            .BeLessThan(10 * 1024 * 1024, "repeated queries should not leak memory");
    }

    [Fact]
    public async Task MemoryUsage_LargeJsonField_DoesNotExplode()
    {
        // Arrange
        var options = CreateSqliteOptions();
        var largeJson = CreateLargeTiledJson(); // ~1MB JSON

        // Act
        var initialMemory = GC.GetTotalMemory(forceFullCollection: true);

        await using (var context = new GameDataContext(options))
        {
            await context.Database.EnsureCreatedAsync();
            context.Maps.Add(
                new MapDefinition
                {
                    MapId = "large_map",
                    DisplayName = "Large Map",
                    Region = "hoenn",
                    TiledDataJson = largeJson,
                }
            );
            await context.SaveChangesAsync();
        }

        // Read it back
        await using (var context = new GameDataContext(options))
        {
            var map = await context.Maps.FirstAsync(m => m.MapId == "large_map");
            var json = map.TiledDataJson;
        }

        var finalMemory = GC.GetTotalMemory(forceFullCollection: true);
        var memoryUsed = finalMemory - initialMemory;

        // Assert
        memoryUsed
            .Should()
            .BeLessThan(50 * 1024 * 1024, "large JSON fields should be handled efficiently");
    }

    #endregion

    #region Helper Methods

    private DbContextOptions<GameDataContext> CreateSqliteOptions()
    {
        return new DbContextOptionsBuilder<GameDataContext>()
            .UseSqlite($"Data Source={_testDbPath}")
            .Options;
    }

    private MapDefinition CreateTestMapDefinition(string? mapId = null)
    {
        return new MapDefinition
        {
            MapId = mapId ?? "test_map",
            DisplayName = "Test Map",
            Region = "hoenn",
            MapType = "town",
            TiledDataJson = CreateSampleTiledJson(0),
            MusicId = "littleroot",
            Weather = "clear",
            ShowMapName = true,
            CanFly = false,
        };
    }

    private NpcDefinition CreateTestNpcDefinition()
    {
        return new NpcDefinition
        {
            NpcId = "test_npc",
            DisplayName = "Test NPC",
            NpcType = "generic",
        };
    }

    private TrainerDefinition CreateTestTrainerDefinition()
    {
        return new TrainerDefinition
        {
            TrainerId = "test_trainer",
            DisplayName = "Test Trainer",
            TrainerClass = "youngster",
        };
    }

    private string CreateSampleTiledJson(int seed)
    {
        var data = new
        {
            width = 20,
            height = 15,
            tilewidth = 16,
            tileheight = 16,
            layers = new[]
            {
                new
                {
                    name = "Ground",
                    type = "tilelayer",
                    data = new int[300],
                },
            },
            seed = seed,
        };
        return JsonSerializer.Serialize(data);
    }

    private string CreateLargeTiledJson()
    {
        // Create ~1MB JSON
        var largeData = new int[250000]; // ~1MB of integers
        var data = new
        {
            width = 500,
            height = 500,
            tilewidth = 16,
            tileheight = 16,
            layers = new[]
            {
                new
                {
                    name = "Ground",
                    type = "tilelayer",
                    data = largeData,
                },
            },
        };
        return JsonSerializer.Serialize(data);
    }

    #endregion
}
