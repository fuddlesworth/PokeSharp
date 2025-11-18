using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Moq;
using PokeSharp.Engine.Scenes;
using Xunit;

namespace PokeSharp.Engine.Scenes.Tests;

/// <summary>
///     Unit tests for SceneManager class.
/// </summary>
public class SceneManagerTests
{
    private static SceneManager CreateSceneManager()
    {
        var graphicsDevice = new Mock<Microsoft.Xna.Framework.Graphics.GraphicsDevice>();
        var services = new Mock<IServiceProvider>();
        var logger = new Mock<ILogger<SceneManager>>();

        // Note: This will fail at runtime without a real GraphicsDevice, but allows unit testing of logic
        return new SceneManager(graphicsDevice.Object, services.Object, logger.Object);
    }

    private static Mock<IScene> CreateMockScene(string name)
    {
        var scene = new Mock<IScene>();
        scene.Setup(s => s.GetType().Name).Returns(name);
        scene.Setup(s => s.IsDisposed).Returns(false);
        scene.Setup(s => s.IsInitialized).Returns(false);
        scene.Setup(s => s.IsContentLoaded).Returns(false);
        return scene;
    }

    [Fact]
    public void ChangeScene_ShouldQueueSceneChange()
    {
        // Arrange
        var manager = CreateSceneManager();
        var scene = CreateMockScene("TestScene");

        // Act
        manager.ChangeScene(scene.Object);

        // Assert - Scene should not be current yet (two-step pattern)
        manager.CurrentScene.Should().BeNull();
    }

    [Fact]
    public void ChangeScene_ShouldTransitionOnNextUpdate()
    {
        // Arrange
        var manager = CreateSceneManager();
        var scene = CreateMockScene("TestScene");
        var gameTime = new GameTime();

        // Act
        manager.ChangeScene(scene.Object);
        manager.Update(gameTime);

        // Assert - Scene should now be current
        manager.CurrentScene.Should().Be(scene.Object);
        scene.Verify(s => s.Initialize(), Times.Once);
        scene.Verify(s => s.LoadContent(), Times.Once);
    }

    [Fact]
    public void ChangeScene_ShouldDisposePreviousScene()
    {
        // Arrange
        var manager = CreateSceneManager();
        var scene1 = CreateMockScene("Scene1");
        var scene2 = CreateMockScene("Scene2");
        var gameTime = new GameTime();

        // Act
        manager.ChangeScene(scene1.Object);
        manager.Update(gameTime);
        manager.ChangeScene(scene2.Object);
        manager.Update(gameTime);

        // Assert
        scene1.Verify(s => s.Dispose(), Times.Once);
        manager.CurrentScene.Should().Be(scene2.Object);
    }

    [Fact]
    public void PushScene_ShouldAddToStack()
    {
        // Arrange
        var manager = CreateSceneManager();
        var baseScene = CreateMockScene("BaseScene");
        var overlayScene = CreateMockScene("OverlayScene");
        var gameTime = new GameTime();

        // Act
        manager.ChangeScene(baseScene.Object);
        manager.Update(gameTime);
        manager.PushScene(overlayScene.Object);
        manager.Update(gameTime);

        // Assert
        manager.CurrentScene.Should().Be(overlayScene.Object); // Stack top
        overlayScene.Verify(s => s.Initialize(), Times.Once);
        overlayScene.Verify(s => s.LoadContent(), Times.Once);
    }

    [Fact]
    public void PopScene_ShouldRemoveFromStack()
    {
        // Arrange
        var manager = CreateSceneManager();
        var baseScene = CreateMockScene("BaseScene");
        var overlayScene = CreateMockScene("OverlayScene");
        var gameTime = new GameTime();

        // Act
        manager.ChangeScene(baseScene.Object);
        manager.Update(gameTime);
        manager.PushScene(overlayScene.Object);
        manager.Update(gameTime);
        manager.PopScene();
        manager.Update(gameTime);

        // Assert
        overlayScene.Verify(s => s.Dispose(), Times.Once);
        manager.CurrentScene.Should().Be(baseScene.Object);
    }

    [Fact]
    public void Update_ShouldUpdateCurrentScene()
    {
        // Arrange
        var manager = CreateSceneManager();
        var scene = CreateMockScene("TestScene");
        var gameTime = new GameTime();

        // Act
        manager.ChangeScene(scene.Object);
        manager.Update(gameTime);
        manager.Update(gameTime); // Second update

        // Assert
        scene.Verify(s => s.Update(gameTime), Times.Once); // Called once after transition
    }

    [Fact]
    public void Draw_ShouldDrawCurrentScene()
    {
        // Arrange
        var manager = CreateSceneManager();
        var scene = CreateMockScene("TestScene");
        var gameTime = new GameTime();

        // Act
        manager.ChangeScene(scene.Object);
        manager.Update(gameTime); // Initialize scene
        manager.Draw(gameTime);

        // Assert
        scene.Verify(s => s.Draw(gameTime), Times.Once);
    }
}

