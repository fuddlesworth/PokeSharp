using FontStashSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PokeSharp.Engine.Scenes.Scenes;

/// <summary>
/// Scene that displays during async initialization.
/// Shows progress bar and current loading step.
/// Uses simple SpriteBatch rendering - no external UI framework dependencies.
/// </summary>
public class LoadingScene : SceneBase
{
    private readonly LoadingProgress _progress;
    private readonly Task<IScene> _initializationTask;
    private readonly SceneManager _sceneManager;

    // Rendering
    private SpriteBatch? _spriteBatch;
    private Texture2D? _pixel;
    private FontSystem? _fontSystem;
    private SpriteFontBase? _font;
    private SpriteFontBase? _fontSmall;

    // Layout constants
    private const int ProgressBarWidth = 600;
    private const int ProgressBarHeight = 30;
    private const int ProgressBarPadding = 4;
    private const int FontSize = 24;
    private const int FontSizeSmall = 18;

    // Colors
    private static readonly Color BackgroundColor = new(20, 20, 25);
    private static readonly Color ProgressBarBackgroundColor = new(40, 40, 45);
    private static readonly Color ProgressBarFillColor = new(200, 60, 60); // Pokemon red
    private static readonly Color ProgressBarBorderColor = new(60, 60, 65);
    private static readonly Color TextColor = Color.White;
    private static readonly Color TextSecondaryColor = new(180, 180, 180);
    private static readonly Color ErrorColor = new(255, 100, 100);

    // State
    private bool _hasCompleted;
    private IScene? _completedScene;

    /// <summary>
    /// Initializes a new instance of the LoadingScene class.
    /// </summary>
    public LoadingScene(
        GraphicsDevice graphicsDevice,
        IServiceProvider services,
        ILogger<LoadingScene> logger,
        LoadingProgress progress,
        Task<IScene> initializationTask,
        SceneManager sceneManager
    )
        : base(graphicsDevice, services, logger)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(initializationTask);
        ArgumentNullException.ThrowIfNull(sceneManager);

        _progress = progress;
        _initializationTask = initializationTask;
        _sceneManager = sceneManager;
    }

    /// <inheritdoc />
    public override void LoadContent()
    {
        base.LoadContent();

        _spriteBatch = new SpriteBatch(GraphicsDevice);

        // Create a 1x1 white pixel texture for drawing rectangles
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        // Initialize FontStashSharp with embedded font
        _fontSystem = new FontSystem();
        var fontLoaded = false;

        // Try to load embedded font, fall back to a basic approach
        try
        {
            // Look for any TTF font in any loaded assembly
            foreach (var loadedAssembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var names = loadedAssembly.GetManifestResourceNames();
                    var fontResource = names.FirstOrDefault(n => n.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase));
                    if (fontResource != null)
                    {
                        using var stream = loadedAssembly.GetManifestResourceStream(fontResource);
                        if (stream != null)
                        {
                            var fontData = new byte[stream.Length];
                            stream.ReadExactly(fontData, 0, fontData.Length);
                            _fontSystem.AddFont(fontData);
                            fontLoaded = true;
                            Logger.LogDebug("Loaded font from {Assembly}: {Resource}", loadedAssembly.GetName().Name, fontResource);
                            break;
                        }
                    }
                }
                catch
                {
                    // Skip assemblies that can't be inspected
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load embedded font, using system default");
        }

        // If no font was loaded, try system fonts
        if (!fontLoaded)
        {
            try
            {
                // Try common system font paths
                var systemFontPaths = new[]
                {
                    "/System/Library/Fonts/Helvetica.ttc",  // macOS
                    "/System/Library/Fonts/SFNSText.ttf",   // macOS
                    "C:\\Windows\\Fonts\\segoeui.ttf",       // Windows
                    "C:\\Windows\\Fonts\\arial.ttf",         // Windows
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf" // Linux
                };

                foreach (var fontPath in systemFontPaths)
                {
                    if (File.Exists(fontPath))
                    {
                        _fontSystem.AddFont(File.ReadAllBytes(fontPath));
                        Logger.LogDebug("Loaded system font: {FontPath}", fontPath);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to load system font");
            }
        }

        _font = _fontSystem.GetFont(FontSize);
        _fontSmall = _fontSystem.GetFont(FontSizeSmall);
    }

    /// <inheritdoc />
    public override void Update(GameTime gameTime)
    {
        // Check if initialization is complete
        if (!_hasCompleted && _initializationTask.IsCompleted)
        {
            _hasCompleted = true;

            if (_initializationTask.IsFaulted)
            {
                var exception = _initializationTask.Exception?.GetBaseException()
                    ?? _initializationTask.Exception
                    ?? new Exception("Unknown initialization error");

                Logger.LogError(exception, "Game initialization failed: {ErrorMessage}", exception.Message);

                _progress.Error = exception;
                _progress.IsComplete = true;
                _progress.CurrentStep = $"Error: {exception.Message}";
            }
            else if (_initializationTask.IsCanceled)
            {
                Logger.LogWarning("Game initialization was cancelled");
                _progress.IsComplete = true;
                _progress.CurrentStep = "Initialization cancelled";
            }
            else
            {
                try
                {
                    _completedScene = _initializationTask.Result;
                    _progress.IsComplete = true;
                    _progress.Progress = 1.0f;
                    _progress.CurrentStep = "Initialization complete!";

                    Logger.LogInformation("Initialization complete, transitioning to gameplay scene");
                    _sceneManager.ChangeScene(_completedScene);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to get completed scene from initialization task");
                    _progress.Error = ex;
                    _progress.IsComplete = true;
                    _progress.CurrentStep = $"Error: {ex.Message}";
                }
            }
        }
    }

    /// <inheritdoc />
    public override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(BackgroundColor);

        if (_spriteBatch == null || _pixel == null)
            return;

        var viewport = GraphicsDevice.Viewport;
        var centerX = viewport.Width / 2;
        var centerY = viewport.Height / 2;

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        // Calculate positions
        var barX = centerX - ProgressBarWidth / 2;
        var barY = centerY;

        // Draw progress bar background
        DrawRectangle(barX, barY, ProgressBarWidth, ProgressBarHeight, ProgressBarBackgroundColor);

        // Draw progress bar border
        DrawRectangleOutline(barX, barY, ProgressBarWidth, ProgressBarHeight, ProgressBarBorderColor, 2);

        // Draw progress bar fill
        var fillWidth = (int)((ProgressBarWidth - ProgressBarPadding * 2) * _progress.Progress);
        if (fillWidth > 0)
        {
            DrawRectangle(
                barX + ProgressBarPadding,
                barY + ProgressBarPadding,
                fillWidth,
                ProgressBarHeight - ProgressBarPadding * 2,
                ProgressBarFillColor
            );
        }

        // Draw percentage text centered on progress bar
        var percentage = (int)(_progress.Progress * 100);
        var percentText = $"{percentage}%";
        if (_font != null)
        {
            var textSize = _font.MeasureString(percentText);
            var textX = centerX - textSize.X / 2;
            var textY = barY + (ProgressBarHeight - textSize.Y) / 2;
            _spriteBatch.DrawString(_font, percentText, new Vector2(textX, textY), TextColor);
        }

        // Draw step text above progress bar
        var stepText = _progress.CurrentStep ?? "Loading...";
        if (_fontSmall != null)
        {
            var stepSize = _fontSmall.MeasureString(stepText);
            var stepX = centerX - stepSize.X / 2;
            var stepY = barY - stepSize.Y - 20;
            _spriteBatch.DrawString(_fontSmall, stepText, new Vector2(stepX, stepY), TextSecondaryColor);
        }

        // Draw title
        const string title = "PokeSharp";
        if (_font != null)
        {
            var titleSize = _font.MeasureString(title);
            var titleX = centerX - titleSize.X / 2;
            var titleY = barY - 100;
            _spriteBatch.DrawString(_font, title, new Vector2(titleX, titleY), TextColor);
        }

        // Draw error message if any
        if (_progress.Error != null && _fontSmall != null)
        {
            var errorText = $"Error: {_progress.Error.Message}";
            var errorSize = _fontSmall.MeasureString(errorText);
            var errorX = centerX - errorSize.X / 2;
            var errorY = barY + ProgressBarHeight + 20;

            // Draw error background
            DrawRectangle((int)errorX - 10, (int)errorY - 5, (int)errorSize.X + 20, (int)errorSize.Y + 10, new Color(80, 0, 0));
            _spriteBatch.DrawString(_fontSmall, errorText, new Vector2(errorX, errorY), ErrorColor);
        }

        _spriteBatch.End();
    }

    private void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        if (_pixel == null || _spriteBatch == null) return;
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height), color);
    }

    private void DrawRectangleOutline(int x, int y, int width, int height, Color color, int thickness)
    {
        if (_pixel == null || _spriteBatch == null) return;

        // Top
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, width, thickness), color);
        // Bottom
        _spriteBatch.Draw(_pixel, new Rectangle(x, y + height - thickness, width, thickness), color);
        // Left
        _spriteBatch.Draw(_pixel, new Rectangle(x, y, thickness, height), color);
        // Right
        _spriteBatch.Draw(_pixel, new Rectangle(x + width - thickness, y, thickness, height), color);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _spriteBatch?.Dispose();
            _pixel?.Dispose();
            _fontSystem?.Dispose();
        }

        base.Dispose(disposing);
    }
}
