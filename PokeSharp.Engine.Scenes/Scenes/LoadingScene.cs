using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum;
using RenderingLibrary.Graphics;
using Color = System.Drawing.Color;

namespace PokeSharp.Engine.Scenes.Scenes;

/// <summary>
///     Scene that displays during async initialization.
///     Shows progress bar and current loading step.
/// </summary>
public class LoadingScene : SceneBase
{
    private readonly Game _game;
    private readonly Task<IScene> _initializationTask;
    private readonly LoadingProgress _progress;
    private readonly SceneManager _sceneManager;
    private IScene? _completedScene;
    private SolidRectangle? _errorBar;
    private Text? _errorText;
    private bool _gumInitialized;
    private GumService? _gumService;
    private bool _hasCompleted;
    private Text? _percentageText;
    private SolidRectangle? _progressBarBackground;
    private SolidRectangle? _progressBarFill;
    private object? _rootContainer; // GumService.Root - type will be inferred
    private Text? _stepText;

    /// <summary>
    ///     Initializes a new instance of the LoadingScene class.
    /// </summary>
    /// <param name="graphicsDevice">The graphics device.</param>
    /// <param name="services">The service provider.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="progress">The loading progress tracker.</param>
    /// <param name="initializationTask">The task that initializes the gameplay scene.</param>
    /// <param name="sceneManager">The scene manager to transition to gameplay when ready.</param>
    /// <param name="game">The game instance (required for Gum initialization).</param>
    public LoadingScene(
        GraphicsDevice graphicsDevice,
        IServiceProvider services,
        ILogger<LoadingScene> logger,
        LoadingProgress progress,
        Task<IScene> initializationTask,
        SceneManager sceneManager,
        Game game
    )
        : base(graphicsDevice, services, logger)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(initializationTask);
        ArgumentNullException.ThrowIfNull(sceneManager);
        ArgumentNullException.ThrowIfNull(game);

        _progress = progress;
        _initializationTask = initializationTask;
        _sceneManager = sceneManager;
        _game = game;
    }

    /// <inheritdoc />
    public override void LoadContent()
    {
        base.LoadContent();

        // Initialize Gum for UI rendering (programmatic, no .gumx file needed)
        try
        {
            _gumService = GumService.Default;
            _gumService.Initialize(_game);

            // Get the SystemManagers to access the rendering system
            var systemManagers = _gumService.SystemManagers;
            if (systemManagers == null)
                throw new InvalidOperationException(
                    "GumService.SystemManagers is null after initialization"
                );

            // Use the Root from GumService
            var root = _gumService.Root;
            if (root == null)
                throw new InvalidOperationException("GumService.Root is null after initialization");
            _rootContainer = root;

            // Ensure root has proper dimensions to avoid layout issues
            var viewport = GraphicsDevice.Viewport;
            if (root.Width == 0 || root.Height == 0)
            {
                root.Width = viewport.Width;
                root.Height = viewport.Height;
            }

            // Configure root to use absolute dimensions instead of relative
            // This prevents SetDimensionsToCanvas from being called every frame
            // which triggers layout updates that can cause null references
            try
            {
                var rootType = root.GetType();
                var widthUnitsProperty = rootType.GetProperty("WidthUnits");
                var heightUnitsProperty = rootType.GetProperty("HeightUnits");

                // Try to set to Absolute to prevent automatic dimension updates
                if (widthUnitsProperty != null)
                {
                    var absoluteValue = Enum.Parse(widthUnitsProperty.PropertyType, "Absolute");
                    widthUnitsProperty.SetValue(root, absoluteValue);
                }

                if (heightUnitsProperty != null)
                {
                    var absoluteValue = Enum.Parse(heightUnitsProperty.PropertyType, "Absolute");
                    heightUnitsProperty.SetValue(root, absoluteValue);
                }
            }
            catch (Exception ex)
            {
                Logger.LogDebug(
                    ex,
                    "Could not set root dimension units, SetDimensionsToCanvas may still be called"
                );
            }

            // Create all elements and set their properties BEFORE adding to collection
            // This prevents reentrancy issues with ObservableCollection

            // Create progress bar background
            _progressBarBackground = new SolidRectangle();
            _progressBarBackground.Width = 600;
            _progressBarBackground.Height = 30;
            _progressBarBackground.X = (viewport.Width - 600) / 2;
            _progressBarBackground.Y = viewport.Height / 2;
            _progressBarBackground.Color = Color.FromArgb(40, 40, 40);

            // Create progress bar fill
            _progressBarFill = new SolidRectangle();
            _progressBarFill.Height = 22; // 30 - 4*2 padding
            _progressBarFill.X = _progressBarBackground.X + 4;
            _progressBarFill.Y = _progressBarBackground.Y + 4;
            _progressBarFill.Color = Color.FromArgb(100, 149, 237); // CornflowerBlue

            // Get or create a default font for text elements
            // Text elements need a font to avoid null reference during layout updates
            SpriteFont? defaultFont = null;
            try
            {
                // Try to get the default font from SystemManagers
                var systemManagersType = systemManagers.GetType();
                var fontManagerProperty =
                    systemManagersType.GetProperty("FontManager")
                    ?? systemManagersType.GetProperty("TextManager");

                if (fontManagerProperty != null)
                {
                    var fontManager = fontManagerProperty.GetValue(systemManagers);
                    if (fontManager != null)
                    {
                        var fontManagerType = fontManager.GetType();
                        var defaultFontProperty =
                            fontManagerType.GetProperty("DefaultFont")
                            ?? fontManagerType.GetProperty("Font");
                        if (defaultFontProperty != null)
                            defaultFont = defaultFontProperty.GetValue(fontManager) as SpriteFont;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning(
                    ex,
                    "Failed to get default font from SystemManagers, text may not render correctly"
                );
            }

            // Create step text (no word wrapping - we have plenty of space)
            _stepText = new Text(systemManagers);
            if (defaultFont != null)
            {
                // Set font using reflection if available
                var textType = _stepText.GetType();
                var fontProperty =
                    textType.GetProperty("Font") ?? textType.GetProperty("SpriteFont");
                fontProperty?.SetValue(_stepText, defaultFont);
            }

            _stepText.X = _progressBarBackground.X;
            _stepText.Y = _progressBarBackground.Y - 40;
            _stepText.Red = 255;
            _stepText.Green = 255;
            _stepText.Blue = 255;
            _stepText.Width = 600; // Set width to prevent wrapping
            // Set initial text to ensure layout can calculate properly
            _stepText.RawText = "Loading...";

            // Create percentage text (overlaps the progress bar, centered horizontally and vertically)
            _percentageText = new Text(systemManagers);
            if (defaultFont != null)
            {
                var textType = _percentageText.GetType();
                var fontProperty =
                    textType.GetProperty("Font") ?? textType.GetProperty("SpriteFont");
                fontProperty?.SetValue(_percentageText, defaultFont);
            }

            // Center vertically on progress bar (approximate center - text height varies)
            _percentageText.Y = _progressBarBackground.Y + _progressBarBackground.Height / 2 - 10;
            // Don't set Width - let Gum calculate it automatically based on text content
            // Center horizontally on progress bar - will be recalculated in Update
            _percentageText.X = _progressBarBackground.X + _progressBarBackground.Width / 2;
            _percentageText.Red = 255;
            _percentageText.Green = 255;
            _percentageText.Blue = 255;
            // Set initial text to ensure layout can calculate properly
            _percentageText.RawText = "0%";

            // Create error bar (initially hidden)
            _errorBar = new SolidRectangle();
            _errorBar.Width = viewport.Width;
            _errorBar.Height = 80;
            _errorBar.X = 0;
            _errorBar.Y = 0;
            _errorBar.Color = Color.FromArgb(80, 0, 0);
            _errorBar.Visible = false;

            // Create error text (initially hidden)
            _errorText = new Text(systemManagers);
            if (defaultFont != null)
            {
                var textType = _errorText.GetType();
                var fontProperty =
                    textType.GetProperty("Font") ?? textType.GetProperty("SpriteFont");
                fontProperty?.SetValue(_errorText, defaultFont);
            }

            _errorText.X = 20;
            _errorText.Y = 20;
            _errorText.Red = 255;
            _errorText.Green = 255;
            _errorText.Blue = 255;
            _errorText.Visible = false;
            // Set initial text to ensure layout can calculate properly
            _errorText.RawText = "";

            // Add all elements to root by setting Parent property
            // IMPORTANT: Add non-text elements first, then text elements
            // This ensures layout calculations have all required properties set
            _progressBarBackground.Parent = root;
            _progressBarFill.Parent = root;
            _errorBar.Parent = root;

            // Add text elements last, after all properties (including font and text) are set
            // This prevents null reference during layout updates
            _stepText.Parent = root;
            _percentageText.Parent = root;
            _errorText.Parent = root;

            // Mark Gum as fully initialized only after all elements are added
            // We do NOT call UpdateLayout() here as it triggers the null reference
            // The layout will be updated naturally during GumService.Update() calls
            _gumInitialized = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize Gum UI");
            _gumService = null;
            _gumInitialized = false;
        }
    }

    /// <inheritdoc />
    public override void Update(GameTime gameTime)
    {
        // Ensure LoadContent has been called before attempting to update Gum
        if (!IsContentLoaded)
            // LoadContent hasn't been called yet - this shouldn't happen but guard against it
            return;

        // Update Gum only if fully initialized and all systems are ready
        // Check that GumService is initialized and all required properties are set
        if (
            _gumInitialized
            && _gumService != null
            && _gumService.SystemManagers != null
            && _gumService.Root != null
            && _progressBarBackground != null
            && IsGumReadyForUpdate()
        )
            try
            {
                _gumService.Update(gameTime);
            }
            catch (NullReferenceException)
            {
                // Silently catch null reference exceptions from Gum's internal layout updates
                // This can happen if Text elements don't have fonts set yet
            }

        // Update UI elements only if Gum is fully initialized
        if (_gumInitialized && _gumService != null)
        {
            // Update progress bar fill width
            if (_progressBarFill != null && _progressBarBackground != null)
            {
                var progressWidth = (int)((_progressBarBackground.Width - 8) * _progress.Progress);
                _progressBarFill.Width = Math.Max(0, progressWidth);
            }

            // Update step text
            if (_stepText != null)
                _stepText.RawText = _progress.CurrentStep ?? string.Empty;

            // Update percentage text (centered horizontally on progress bar)
            if (_percentageText != null && _progressBarBackground != null && _gumService != null)
            {
                var percentage = (int)(_progress.Progress * 100);
                var percentageText = $"{percentage}%";
                _percentageText.RawText = percentageText;

                // Calculate progress bar center
                var progressBarCenterX =
                    _progressBarBackground.X + _progressBarBackground.Width / 2;

                // Get actual text width using SpriteFont.MeasureString
                float textWidth = 0;

                if (_percentageText is Text textElement && _gumService.SystemManagers != null)
                {
                    // Try to get the SpriteFont from the Text element or SystemManagers
                    var textType = textElement.GetType();
                    var fontProperty =
                        textType.GetProperty("Font") ?? textType.GetProperty("SpriteFont");

                    if (fontProperty != null)
                    {
                        var font = fontProperty.GetValue(textElement);
                        if (font is SpriteFont spriteFont)
                            textWidth = spriteFont.MeasureString(percentageText).X;
                    }
                    else
                    {
                        // Try to get font from SystemManagers
                        var systemManagersType = _gumService.SystemManagers.GetType();
                        var fontManagerProperty =
                            systemManagersType.GetProperty("FontManager")
                            ?? systemManagersType.GetProperty("TextManager");

                        if (fontManagerProperty != null)
                        {
                            var fontManager = fontManagerProperty.GetValue(
                                _gumService.SystemManagers
                            );
                            if (fontManager != null)
                            {
                                var fontManagerType = fontManager.GetType();
                                var defaultFontProperty =
                                    fontManagerType.GetProperty("DefaultFont")
                                    ?? fontManagerType.GetProperty("Font");
                                if (defaultFontProperty != null)
                                {
                                    var font = defaultFontProperty.GetValue(fontManager);
                                    if (font is SpriteFont spriteFont)
                                        textWidth = spriteFont.MeasureString(percentageText).X;
                                }
                            }
                        }
                    }
                }

                // Center by subtracting half the text width from the progress bar center
                if (textWidth > 0)
                {
                    var newX = progressBarCenterX - textWidth / 2;
                    _percentageText.X = newX;
                }
            }

            // Update error display
            if (_errorBar != null && _errorText != null)
            {
                var hasError = _progress.Error != null;
                _errorBar.Visible = hasError;
                _errorText.Visible = hasError;
                if (hasError)
                    _errorText.RawText = $"Error: {_progress.Error!.Message}";
            }
        }

        // Check if initialization is complete
        if (!_hasCompleted && _initializationTask.IsCompleted)
        {
            _hasCompleted = true;

            if (_initializationTask.IsFaulted)
            {
                var exception =
                    _initializationTask.Exception?.GetBaseException()
                    ?? _initializationTask.Exception
                    ?? new Exception("Unknown initialization error");

                Logger.LogError(
                    exception,
                    "Game initialization failed: {ErrorMessage}",
                    exception.Message
                );

                _progress.Error = exception;
                _progress.IsComplete = true;
                _progress.CurrentStep = $"Error: {exception.Message}";

                // Don't transition - stay on loading scene to show error
            }
            else if (_initializationTask.IsCanceled)
            {
                Logger.LogWarning("Game initialization was cancelled");
                _progress.IsComplete = true;
                _progress.CurrentStep = "Initialization cancelled";
            }
            else
            {
                // Initialization succeeded
                try
                {
                    _completedScene = _initializationTask.Result;
                    _progress.IsComplete = true;
                    _progress.Progress = 1.0f;
                    _progress.CurrentStep = "Initialization complete!";

                    // Transition to gameplay scene
                    Logger.LogInformation(
                        "Initialization complete, transitioning to gameplay scene"
                    );
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
        // Clear screen
        GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);

        // Draw Gum UI
        _gumService?.Draw();
    }

    /// <summary>
    ///     Checks if Gum is ready for updates by verifying that all Text elements have fonts set.
    ///     This prevents null reference exceptions during layout updates.
    /// </summary>
    private bool IsGumReadyForUpdate()
    {
        if (_gumService?.SystemManagers == null)
            return false;

        // Check if Text elements have fonts initialized
        // Text elements without fonts cause null references during layout updates
        try
        {
            var systemManagersType = _gumService.SystemManagers.GetType();
            var fontManagerProperty =
                systemManagersType.GetProperty("FontManager")
                ?? systemManagersType.GetProperty("TextManager");

            if (fontManagerProperty == null)
                return true; // Can't check, assume ready

            var fontManager = fontManagerProperty.GetValue(_gumService.SystemManagers);
            if (fontManager == null)
                return false; // Font manager not initialized

            // Check if we have a default font available
            var fontManagerType = fontManager.GetType();
            var defaultFontProperty =
                fontManagerType.GetProperty("DefaultFont") ?? fontManagerType.GetProperty("Font");
            if (defaultFontProperty == null)
                return true; // Can't check, assume ready

            var defaultFont = defaultFontProperty.GetValue(fontManager);
            if (defaultFont == null)
                return false; // No default font available yet

            // Check if Text elements have fonts set
            var textElements = new[] { _stepText, _percentageText, _errorText };
            foreach (var textElement in textElements)
            {
                if (textElement == null)
                    continue;

                var textType = textElement.GetType();
                var fontProperty =
                    textType.GetProperty("Font") ?? textType.GetProperty("SpriteFont");
                if (fontProperty != null)
                {
                    var font = fontProperty.GetValue(textElement);
                    if (font == null)
                        return false; // Text element doesn't have a font set yet
                }
            }

            return true; // All checks passed
        }
        catch
        {
            // If we can't check, assume ready to avoid blocking updates
            return true;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up Gum UI elements by setting Parent to null
            // This avoids ObservableCollection reentrancy issues
            if (_progressBarBackground != null)
                _progressBarBackground.Parent = null;
            if (_progressBarFill != null)
                _progressBarFill.Parent = null;
            if (_stepText != null)
                _stepText.Parent = null;
            if (_percentageText != null)
                _percentageText.Parent = null;
            if (_errorBar != null)
                _errorBar.Parent = null;
            if (_errorText != null)
                _errorText.Parent = null;
        }

        base.Dispose(disposing);
    }
}
