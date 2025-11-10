using Arch.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PokeSharp.Core.Parallel;
using PokeSharp.Core.Systems;
using Xunit;
using Xunit.Abstractions;

namespace PokeSharp.Core.Tests.Parallel;

/// <summary>
///     Tests for ParallelSystemManager to ensure all systems are included in execution plan.
/// </summary>
public class ParallelSystemManagerTests
{
    private readonly ITestOutputHelper _output;

    public ParallelSystemManagerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    ///     Test system that implements IUpdateSystem for testing parallel execution.
    /// </summary>
    private class TestUpdateSystem : IUpdateSystem
    {
        public int UpdatePriority => 100;
        public int Priority => 100;
        public bool Enabled { get; set; } = true;

        public void Update(World world, float deltaTime)
        {
            // Do nothing
        }

        public void Initialize(World world)
        {
            // Do nothing
        }
    }

    /// <summary>
    ///     Test system that implements IRenderSystem for testing parallel execution.
    /// </summary>
    private class TestRenderSystem : IRenderSystem
    {
        public int RenderOrder => 100;
        public int Priority => 100;
        public bool Enabled { get; set; } = true;

        public void Render(World world)
        {
            // Do nothing
        }

        public void Update(World world, float deltaTime)
        {
            // Do nothing - IRenderSystem inherits from ISystem
        }

        public void Initialize(World world)
        {
            // Do nothing
        }
    }

    [Fact]
    public void RegisterUpdateSystem_ShouldBeIncludedInExecutionPlan()
    {
        // Arrange
        var world = World.Create();
        var logger = NullLogger<ParallelSystemManager>.Instance;
        var manager = new ParallelSystemManager(world, enableParallel: true, logger);

        // Act
        manager.RegisterUpdateSystem(new TestUpdateSystem());
        manager.RebuildExecutionPlan();

        // Assert
        var executionPlan = manager.GetExecutionPlan();
        _output.WriteLine("Execution Plan:");
        _output.WriteLine(executionPlan);

        // Should have at least 1 stage with the system
        Assert.Contains("TestUpdateSystem", executionPlan);
        Assert.DoesNotContain("Execution plan not built", executionPlan);
    }

    [Fact]
    public void RegisterRenderSystem_ShouldBeIncludedInExecutionPlan()
    {
        // Arrange
        var world = World.Create();
        var logger = NullLogger<ParallelSystemManager>.Instance;
        var manager = new ParallelSystemManager(world, enableParallel: true, logger);

        // Act
        manager.RegisterRenderSystem(new TestRenderSystem());
        manager.RebuildExecutionPlan();

        // Assert
        var executionPlan = manager.GetExecutionPlan();
        _output.WriteLine("Execution Plan:");
        _output.WriteLine(executionPlan);

        // Should have at least 1 stage with the system
        Assert.Contains("TestRenderSystem", executionPlan);
        Assert.DoesNotContain("Execution plan not built", executionPlan);
    }

    [Fact]
    public void RegisterMultipleSystemTypes_ShouldAllBeIncludedInExecutionPlan()
    {
        // Arrange
        var world = World.Create();
        var logger = NullLogger<ParallelSystemManager>.Instance;
        var manager = new ParallelSystemManager(world, enableParallel: true, logger);

        // Act
        manager.RegisterUpdateSystem(new TestUpdateSystem());
        manager.RegisterRenderSystem(new TestRenderSystem());
        manager.RebuildExecutionPlan();

        // Assert
        var executionPlan = manager.GetExecutionPlan();
        _output.WriteLine("Execution Plan:");
        _output.WriteLine(executionPlan);

        // Should have both systems in the plan
        Assert.Contains("TestUpdateSystem", executionPlan);
        Assert.Contains("TestRenderSystem", executionPlan);
        Assert.DoesNotContain("Execution plan not built", executionPlan);
    }

    [Fact]
    public void DependencyGraph_ShouldContainAllRegisteredSystems()
    {
        // Arrange
        var world = World.Create();
        var logger = NullLogger<ParallelSystemManager>.Instance;
        var manager = new ParallelSystemManager(world, enableParallel: true, logger);

        // Act
        manager.RegisterUpdateSystem(new TestUpdateSystem());
        manager.RegisterRenderSystem(new TestRenderSystem());
        manager.RebuildExecutionPlan();

        // Assert
        var dependencyGraph = manager.GetDependencyGraph();
        _output.WriteLine("Dependency Graph:");
        _output.WriteLine(dependencyGraph);

        // Verify both systems are in the dependency graph
        Assert.Contains("TestUpdateSystem", dependencyGraph);
        Assert.Contains("TestRenderSystem", dependencyGraph);
    }
}
