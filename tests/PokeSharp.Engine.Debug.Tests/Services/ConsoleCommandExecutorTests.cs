using Microsoft.Extensions.Logging;
using Moq;
using PokeSharp.Engine.Debug.DebugConsole;
using PokeSharp.Engine.Debug.Scripting;
using PokeSharp.Engine.Debug.Systems.Services;
using PokeSharp.Game.Scripting.Api;

namespace PokeSharp.Engine.Debug.Tests.Services;

public class ConsoleCommandExecutorTests
{
    private readonly Mock<QuakeConsole> _mockConsole;
    private readonly Mock<ConsoleScriptEvaluator> _mockEvaluator;
    private readonly Mock<ConsoleGlobals> _mockGlobals;
    private readonly Mock<AliasMacroManager> _mockAliasManager;
    private readonly Mock<ScriptManager> _mockScriptManager;
    private readonly Mock<ILogger<ConsoleCommandExecutor>> _mockLogger;
    private readonly ConsoleCommandExecutor _executor;

    public ConsoleCommandExecutorTests()
    {
        // Create mocks
        _mockConsole = new Mock<QuakeConsole>(null!, 800, 600, new ConsoleConfig());
        _mockEvaluator = new Mock<ConsoleScriptEvaluator>(
            Mock.Of<ILogger<ConsoleScriptEvaluator>>()
        );
        _mockGlobals = new Mock<ConsoleGlobals>(
            Mock.Of<IScriptingApiProvider>(),
            Mock.Of<Arch.Core.World>(),
            Mock.Of<PokeSharp.Engine.Systems.Management.SystemManager>(),
            null!,
            Mock.Of<ILogger<ConsoleGlobals>>()
        );
        _mockAliasManager = new Mock<AliasMacroManager>(
            "test_aliases.txt",
            Mock.Of<ILogger<AliasMacroManager>>()
        );
        _mockScriptManager = new Mock<ScriptManager>(null, Mock.Of<ILogger<ScriptManager>>());
        _mockLogger = new Mock<ILogger<ConsoleCommandExecutor>>();

        // Create executor
        _executor = new ConsoleCommandExecutor(
            _mockConsole.Object,
            _mockEvaluator.Object,
            _mockGlobals.Object,
            _mockAliasManager.Object,
            _mockScriptManager.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyCommand_ReturnsSuccessNoOutput()
    {
        // Act
        var result = await _executor.ExecuteAsync("");

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsBuiltInCommand);
        Assert.Null(result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitespaceCommand_ReturnsSuccessNoOutput()
    {
        // Act
        var result = await _executor.ExecuteAsync("   ");

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsBuiltInCommand);
        Assert.Null(result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WithClearCommand_ReturnsSuccess()
    {
        // Arrange
        _mockConsole.Setup(c => c.ClearOutput()).Verifiable();

        // Act
        var result = await _executor.ExecuteAsync("clear");

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsBuiltInCommand);
        _mockConsole.Verify(c => c.ClearOutput(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithResetCommand_ReturnsSuccess()
    {
        // Arrange
        _mockEvaluator.Setup(e => e.Reset()).Verifiable();

        // Act
        var result = await _executor.ExecuteAsync("reset");

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsBuiltInCommand);
        _mockEvaluator.Verify(e => e.Reset(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithHelpCommand_ShowsHelp()
    {
        // Arrange
        _mockConsole
            .Setup(c =>
                c.AppendOutput(It.IsAny<string>(), It.IsAny<Microsoft.Xna.Framework.Color>())
            )
            .Verifiable();

        // Act
        var result = await _executor.ExecuteAsync("help");

        // Assert
        Assert.True(result.Success);
        Assert.True(result.IsBuiltInCommand);
        // Verify help text was output (at least 10 lines of help text)
        _mockConsole.Verify(
            c => c.AppendOutput(It.IsAny<string>(), It.IsAny<Microsoft.Xna.Framework.Color>()),
            Times.AtLeast(10)
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithAliasExpansion_ExpandsAndExecutes()
    {
        // Arrange
        string expandedCommand = "Player.SetPosition(100, 200)";
        _mockAliasManager
            .Setup(m => m.TryExpandAlias("tp 100 200", out expandedCommand))
            .Returns(true);

        _mockEvaluator
            .Setup(e => e.EvaluateAsync(expandedCommand, _mockGlobals.Object))
            .ReturnsAsync("OK");

        // Act
        var result = await _executor.ExecuteAsync("tp 100 200");

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsBuiltInCommand);
        _mockEvaluator.Verify(
            e => e.EvaluateAsync(expandedCommand, _mockGlobals.Object),
            Times.Once
        );
    }

    [Fact]
    public async Task ExecuteAsync_WithScriptCode_EvaluatesAndReturnsResult()
    {
        // Arrange
        string code = "2 + 2";
        string expectedResult = "4";

        _mockEvaluator
            .Setup(e => e.EvaluateAsync(code, _mockGlobals.Object))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _executor.ExecuteAsync(code);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsBuiltInCommand);
        Assert.Equal(expectedResult, result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WithScriptReturningNull_ReturnsSuccessNoOutput()
    {
        // Arrange
        string code = "void operation";

        _mockEvaluator.Setup(e => e.EvaluateAsync(code, _mockGlobals.Object)).ReturnsAsync("null");

        // Act
        var result = await _executor.ExecuteAsync(code);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.IsBuiltInCommand);
        Assert.Null(result.Output);
    }

    [Fact]
    public async Task ExecuteAsync_WithException_ReturnsFailure()
    {
        // Arrange
        string code = "throw new Exception()";
        var exception = new Exception("Test error");

        _mockEvaluator
            .Setup(e => e.EvaluateAsync(code, _mockGlobals.Object))
            .ThrowsAsync(exception);

        // Act
        var result = await _executor.ExecuteAsync(code);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal(exception, result.Error);
    }
}
