using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PokeSharp.Core.Logging;
using PokeSharp.Game;

// Ensure glyph-heavy logging renders correctly
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

// Setup DI container
var services = new ServiceCollection();

// Add fancy Spectre.Console logging instead of standard console logging
var loggerFactory = ConsoleLoggerFactory.Create();
services.AddSingleton<ILoggerFactory>(loggerFactory);
services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

// Add game services
services.AddGameServices();

// Add the game itself
services.AddSingleton<PokeSharpGame>();

// Build service provider
var serviceProvider = services.BuildServiceProvider();

// Create and run the game
using var game = serviceProvider.GetRequiredService<PokeSharpGame>();
game.Run();
