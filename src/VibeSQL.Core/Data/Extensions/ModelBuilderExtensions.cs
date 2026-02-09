using System;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace VibeSQL.Core.Data.Extensions
{
    /// <summary>
    /// Extensions for ModelBuilder to support dynamic configuration loading by schema namespace.
    /// Discovers and applies IEntityTypeConfiguration classes organized under EntityConfigurations/{Schema}/ folders.
    /// </summary>
    public static class ModelBuilderExtensions
    {
        /// <summary>
        /// Applies all IEntityTypeConfiguration classes from the given schemas' assemblies
        /// </summary>
        /// <param name="modelBuilder">The model builder instance</param>
        /// <param name="schemas">One or more schema names to load configurations from</param>
        public static void ApplySchemaConfigurations(this ModelBuilder modelBuilder, params string[] schemas)
        {
            if (schemas == null || schemas.Length == 0)
            {
                throw new ArgumentException("At least one schema must be specified", nameof(schemas));
            }

            foreach (var schema in schemas)
            {
                modelBuilder.ApplyConfigurationsFromSchema(schema);
            }
        }

        /// <summary>
        /// Applies configurations from a specific schema
        /// </summary>
        private static void ApplyConfigurationsFromSchema(this ModelBuilder modelBuilder, string schema)
        {
            // Only match configs in 'VibeSQL.Core.Data.EntityConfigurations.{schema}' and subfolders
            var root = $"VibeSQL.Core.Data.EntityConfigurations.{schema}";

            var configTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface
                    && t.Name.EndsWith("Configuration")
                    && (t.Namespace == root || t.Namespace?.StartsWith(root + ".") == true)
                    && t.GetInterfaces()
                        .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)))
                .ToList();

            if (!configTypes.Any())
            {
                throw new InvalidOperationException($"No configuration classes found for schema '{schema}'");
            }

            foreach (var type in configTypes)
            {
                try
                {
                    var configuration = Activator.CreateInstance(type);
                    modelBuilder.ApplyConfiguration(configuration as dynamic);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to apply configuration for type {type.Name}. Error: {ex.Message}", ex);
                }
            }
        }
    }
}
