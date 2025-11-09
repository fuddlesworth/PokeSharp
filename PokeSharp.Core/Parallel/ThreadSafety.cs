using System.Reflection;

namespace PokeSharp.Core.Parallel;

/// <summary>
///     Utilities for ensuring thread-safe ECS operations.
///     Validates component types and system dependencies for parallel execution.
/// </summary>
public static class ThreadSafety
{
    /// <summary>
    ///     Check if component type is thread-safe for parallel read.
    ///     Value types (structs) are generally thread-safe for read operations.
    /// </summary>
    public static bool IsThreadSafeRead<T>() where T : struct
    {
        // All value types are thread-safe for read
        return true;
    }

    /// <summary>
    ///     Check if component type is thread-safe for parallel write.
    ///     Only safe if component contains no reference types or mutable shared state.
    /// </summary>
    public static bool IsThreadSafeWrite<T>() where T : struct
    {
        var type = typeof(T);
        return IsThreadSafeWriteType(type);
    }

    /// <summary>
    ///     Check if a type is thread-safe for writes.
    /// </summary>
    public static bool IsThreadSafeWriteType(Type type)
    {
        // Primitive types are thread-safe
        if (type.IsPrimitive || type.IsEnum)
            return true;

        // String is immutable, so thread-safe
        if (type == typeof(string))
            return true;

        // Arrays are not thread-safe for write
        if (type.IsArray)
            return false;

        // Classes are not thread-safe (reference types)
        if (type.IsClass)
            return false;

        // Check all fields recursively
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var field in fields)
        {
            var fieldType = field.FieldType;

            // Classes and arrays make it unsafe
            if (fieldType.IsClass && fieldType != typeof(string))
                return false;

            if (fieldType.IsArray)
                return false;

            // Recursively check struct fields
            if (fieldType.IsValueType && !fieldType.IsPrimitive && !fieldType.IsEnum)
            {
                if (!IsThreadSafeWriteType(fieldType))
                    return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Validate that two systems can run in parallel without data races.
    /// </summary>
    public static bool ValidateSystemDependencies(
        SystemMetadata system1,
        SystemMetadata system2)
    {
        ArgumentNullException.ThrowIfNull(system1);
        ArgumentNullException.ThrowIfNull(system2);

        // If both write the same component, not thread-safe
        var writeConflict = system1.WritesComponents
            .Intersect(system2.WritesComponents)
            .Any();

        if (writeConflict)
        {
            return false;
        }

        // If one writes what the other reads, not thread-safe (read-write conflict)
        var readWriteConflict =
            system1.WritesComponents.Intersect(system2.ReadsComponents).Any() ||
            system2.WritesComponents.Intersect(system1.ReadsComponents).Any();

        if (readWriteConflict)
        {
            return false;
        }

        // Both systems can read the same component safely
        return true;
    }

    /// <summary>
    ///     Analyze component type for thread safety issues.
    /// </summary>
    public static ThreadSafetyAnalysis AnalyzeComponentType<T>() where T : struct
    {
        return AnalyzeComponentType(typeof(T));
    }

    /// <summary>
    ///     Analyze component type for thread safety issues.
    /// </summary>
    public static ThreadSafetyAnalysis AnalyzeComponentType(Type type)
    {
        var analysis = new ThreadSafetyAnalysis
        {
            ComponentType = type,
            IsThreadSafeRead = true, // Reads are always safe for value types
            IsThreadSafeWrite = IsThreadSafeWriteType(type)
        };

        // Analyze fields
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (var field in fields)
        {
            var fieldType = field.FieldType;

            if (fieldType.IsClass && fieldType != typeof(string))
            {
                analysis.UnsafeFields.Add(new FieldSafetyInfo
                {
                    FieldName = field.Name,
                    FieldType = fieldType,
                    Reason = "Reference type (class) - not thread-safe for concurrent writes"
                });
            }
            else if (fieldType.IsArray)
            {
                analysis.UnsafeFields.Add(new FieldSafetyInfo
                {
                    FieldName = field.Name,
                    FieldType = fieldType,
                    Reason = "Array - not thread-safe for concurrent writes"
                });
            }
        }

        return analysis;
    }

    /// <summary>
    ///     Get recommendations for making a component thread-safe.
    /// </summary>
    public static List<string> GetThreadSafetyRecommendations(Type componentType)
    {
        var recommendations = new List<string>();
        var analysis = AnalyzeComponentType(componentType);

        if (analysis.IsThreadSafeWrite)
        {
            recommendations.Add($"Component {componentType.Name} is already thread-safe for parallel writes.");
            return recommendations;
        }

        recommendations.Add($"Component {componentType.Name} has thread-safety issues:");

        foreach (var field in analysis.UnsafeFields)
        {
            recommendations.Add($"  - Field '{field.FieldName}' ({field.FieldType.Name}): {field.Reason}");

            if (field.FieldType.IsClass && field.FieldType != typeof(string))
            {
                recommendations.Add($"    → Consider using a struct instead of class for {field.FieldName}");
            }
            else if (field.FieldType.IsArray)
            {
                recommendations.Add($"    → Replace array with fixed-size buffer or use separate entities");
            }
        }

        recommendations.Add("\nGeneral recommendations:");
        recommendations.Add("  1. Use only value types (structs) in components");
        recommendations.Add("  2. Avoid arrays, use fixed-size buffers if needed");
        recommendations.Add("  3. Avoid string fields, or make them readonly");
        recommendations.Add("  4. For complex data, split into multiple components");

        return recommendations;
    }

    /// <summary>
    ///     Validate all components used by a system are thread-safe.
    /// </summary>
    public static SystemThreadSafetyValidation ValidateSystemThreadSafety(SystemMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var validation = new SystemThreadSafetyValidation
        {
            SystemType = metadata.SystemType,
            IsThreadSafe = true
        };

        // Check all components written by the system
        foreach (var componentType in metadata.WritesComponents)
        {
            if (!IsThreadSafeWriteType(componentType))
            {
                validation.IsThreadSafe = false;
                validation.UnsafeComponents.Add(componentType);

                var analysis = AnalyzeComponentType(componentType);
                foreach (var field in analysis.UnsafeFields)
                {
                    validation.Issues.Add(
                        $"Component {componentType.Name}.{field.FieldName}: {field.Reason}"
                    );
                }
            }
        }

        return validation;
    }
}

/// <summary>
///     Analysis result for component thread safety.
/// </summary>
public class ThreadSafetyAnalysis
{
    public Type ComponentType { get; set; } = null!;
    public bool IsThreadSafeRead { get; set; }
    public bool IsThreadSafeWrite { get; set; }
    public List<FieldSafetyInfo> UnsafeFields { get; set; } = new();

    public override string ToString()
    {
        var status = IsThreadSafeWrite ? "SAFE" : "UNSAFE";
        return $"{ComponentType.Name}: {status} for parallel writes ({UnsafeFields.Count} unsafe fields)";
    }
}

/// <summary>
///     Information about a field that may not be thread-safe.
/// </summary>
public class FieldSafetyInfo
{
    public string FieldName { get; set; } = string.Empty;
    public Type FieldType { get; set; } = null!;
    public string Reason { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{FieldName} ({FieldType.Name}): {Reason}";
    }
}

/// <summary>
///     Validation result for system thread safety.
/// </summary>
public class SystemThreadSafetyValidation
{
    public Type SystemType { get; set; } = null!;
    public bool IsThreadSafe { get; set; }
    public List<Type> UnsafeComponents { get; set; } = new();
    public List<string> Issues { get; set; } = new();

    public string GetReport()
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine($"Thread Safety Analysis for {SystemType.Name}");
        report.AppendLine($"Status: {(IsThreadSafe ? "SAFE" : "UNSAFE")}");

        if (!IsThreadSafe)
        {
            report.AppendLine("\nUnsafe Components:");
            foreach (var component in UnsafeComponents)
            {
                report.AppendLine($"  - {component.Name}");
            }

            report.AppendLine("\nIssues:");
            foreach (var issue in Issues)
            {
                report.AppendLine($"  - {issue}");
            }
        }

        return report.ToString();
    }
}

/// <summary>
///     Attribute to mark a system as requiring exclusive (non-parallel) execution.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class RequiresExclusiveExecutionAttribute : Attribute
{
    public string Reason { get; }

    public RequiresExclusiveExecutionAttribute(string reason)
    {
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
    }
}

/// <summary>
///     Attribute to mark a component as thread-safe for parallel writes.
/// </summary>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public class ThreadSafeComponentAttribute : Attribute
{
    public string Notes { get; set; } = string.Empty;
}
