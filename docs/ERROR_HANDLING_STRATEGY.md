# Error Handling Strategy

This document defines the standardized error handling patterns used throughout the PokeSharp codebase.

## Principles

1. **Fail Fast for Invalid Input**: Use exceptions for programming errors (null arguments, invalid state)
2. **Graceful Degradation for Runtime Errors**: Return null/empty results for recoverable errors
3. **Log All Errors**: Always log errors before returning null or throwing exceptions
4. **Isolate Errors**: Don't let errors in one component break the entire system

## Patterns

### 1. Argument Validation

**Use exceptions for invalid arguments:**

```csharp
public void DoSomething(string value)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(value);
    ArgumentNullException.ThrowIfNull(someObject);
    // ...
}
```

**When to use:**
- Constructor parameters
- Public method parameters
- Required dependencies

### 2. Return Null for Recoverable Errors

**Use null return for operations that can fail gracefully:**

```csharp
public async Task<Entity?> LoadMap(MapIdentifier mapId)
{
    try
    {
        // Load map...
        return mapInfoEntity;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to load map: {MapId}", mapId.Value);
        return null; // Graceful degradation - game continues without map
    }
}
```

**When to use:**
- Resource loading (maps, scripts, assets)
- Optional operations
- Operations where the caller can handle null

**Guidelines:**
- Always log the error before returning null
- Document in XML comments that null can be returned
- Use nullable return types (`T?`, `Task<T?>`)

### 3. Throw Exceptions for Critical Errors

**Use exceptions for errors that should stop execution:**

```csharp
public Entity SpawnFromTemplate(string templateId, World world)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
    
    var template = _templateCache.Get(templateId);
    if (template == null)
    {
        _logger.LogError("Template '{TemplateId}' not found", templateId);
        throw new ArgumentException(
            $"Template '{templateId}' not found in cache",
            nameof(templateId)
        );
    }
    // ...
}
```

**When to use:**
- Invalid state that prevents operation
- Missing required resources that break functionality
- Configuration errors
- Programming errors

### 4. Error Isolation

**Isolate errors in event handlers and background operations:**

```csharp
foreach (var handler in handlers.Values)
    try
    {
        handler(eventData);
    }
    catch (Exception ex)
    {
        // Isolate handler errors - don't let them break event publishing
        _logger.LogError(ex, "Error in event handler for {EventType}", eventType.Name);
        // Continue processing other handlers
    }
```

**When to use:**
- Event handlers
- Background tasks
- Iterating over collections
- Non-critical operations

### 5. Disposal Error Handling

**Log disposal errors but don't throw (unless critical):**

```csharp
public async ValueTask DisposeAsync()
{
    foreach (var resource in _resources)
        try
        {
            if (resource is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (resource is IDisposable disposable)
                disposable.Dispose();
        }
        catch (Exception ex)
        {
            // Log disposal errors but continue disposing other resources
            _logger.LogError(ex, "Error disposing resource of type {Type}", resource.GetType().Name);
        }
}
```

**Guidelines:**
- Always try to dispose all resources
- Log errors but continue
- Only throw if it's a critical resource that must be disposed

## Error Types

### ArgumentException / ArgumentNullException
- **Use for**: Invalid method parameters
- **Example**: Null or empty string passed to method requiring non-empty string

### InvalidOperationException
- **Use for**: Invalid object state
- **Example**: Calling method before initialization, using disposed object

### FileNotFoundException / DirectoryNotFoundException
- **Use for**: Missing files/directories (when file is required)
- **Note**: If file is optional, return null instead

### AggregateException
- **Use for**: Multiple errors collected during operation
- **Example**: Disposing multiple resources where some fail

## Logging Guidelines

1. **Always log errors before returning null or throwing**
2. **Use appropriate log levels:**
   - `LogError`: Recoverable errors, missing resources
   - `LogWarning`: Degraded functionality, fallback behavior
   - `LogCritical`: Fatal errors that may crash the application
3. **Include context in log messages:**
   - Resource identifiers (map ID, script path, etc.)
   - Operation being performed
   - Relevant state information

## Examples

### Good: Graceful Degradation
```csharp
public async Task<object?> LoadScriptAsync(string scriptPath)
{
    if (string.IsNullOrWhiteSpace(scriptPath))
        throw new ArgumentException("Script path cannot be null or empty", nameof(scriptPath));

    var fullPath = Path.Combine(_scriptsBasePath, scriptPath);
    if (!File.Exists(fullPath))
    {
        _logger.LogError("Script file not found: {Path}", fullPath);
        return null; // Graceful - game continues without script
    }

    try
    {
        // Load and compile script...
        return instance;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error loading script {Path}", scriptPath);
        return null; // Graceful - game continues
    }
}
```

### Good: Fail Fast
```csharp
public Entity SpawnFromTemplate(string templateId, World world)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
    ArgumentNullException.ThrowIfNull(world);

    var template = _templateCache.Get(templateId);
    if (template == null)
    {
        _logger.LogError("Template '{TemplateId}' not found", templateId);
        throw new ArgumentException(
            $"Template '{templateId}' not found in cache",
            nameof(templateId)
        ); // Fail fast - this is a programming error
    }
    // ...
}
```

### Good: Error Isolation
```csharp
public void Update(GameTime gameTime)
{
    // Update all systems, but don't let one failure break others
    foreach (var system in _systems)
        try
        {
            system.Update(gameTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating system {SystemType}", system.GetType().Name);
            // Continue with other systems
        }
}
```

## Summary

- **Exceptions**: Invalid arguments, critical errors, programming mistakes
- **Null Returns**: Recoverable errors, optional operations, graceful degradation
- **Error Isolation**: Event handlers, background tasks, non-critical operations
- **Always Log**: Every error should be logged with appropriate context
- **Document**: Use XML comments to document when methods can return null or throw

