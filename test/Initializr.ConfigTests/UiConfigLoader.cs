// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Steeltoe.InitializrService.Config;
using YamlDotNet.RepresentationModel;

namespace Steeltoe.Initializr.ConfigTests
{
    /// <summary>
    /// Loads the Initializr UI configuration the same way InitializrService does when backed by Spring Cloud Config
    /// Server.
    /// </summary>
    /// <remarks>
    /// Config Server serves <c>SteeltoeInitializr.yaml</c> overlaid with <c>SteeltoeInitializr-{environment}.yaml</c>,
    /// both flattened to individual keys (lists become indexed keys). InitializrService then binds the merged keys to
    /// <see cref="UiConfig"/>. Because the overlay happens per key, a profile list that is shorter than the matching
    /// base list still contains the trailing base entries.
    /// </remarks>
    public static class UiConfigLoader
    {
        public const string BaseFileName = "SteeltoeInitializr.yaml";

        private const string ProfileFilePrefix = "SteeltoeInitializr-";

        private const string FileExtension = ".yaml";

        /// <summary>
        /// Gets the environment names for which a profile file exists, e.g. <c>Staging</c>.
        /// </summary>
        public static IEnumerable<string> GetEnvironments()
        {
            return Directory.GetFiles(AppContext.BaseDirectory, $"{ProfileFilePrefix}*{FileExtension}")
                .Select(Path.GetFileName)
                .Select(name => name[ProfileFilePrefix.Length..^FileExtension.Length])
                .Order(StringComparer.Ordinal);
        }

        /// <summary>
        /// Gets the flattened settings of the base file, shared by all environments.
        /// </summary>
        public static IDictionary<string, string> LoadBaseSettings()
        {
            return LoadSettings(BaseFileName);
        }

        /// <summary>
        /// Gets the flattened settings of the profile file for the specified environment.
        /// </summary>
        public static IDictionary<string, string> LoadProfileSettings(string environment)
        {
            return LoadSettings(GetProfileFileName(environment));
        }

        /// <summary>
        /// Gets the effective configuration for the specified environment.
        /// </summary>
        /// <param name="environment">The environment name, e.g. <c>Staging</c>.</param>
        /// <param name="errorOnUnknownSettings">
        /// Whether to fail on settings that do not map to a <see cref="UiConfig"/> property.
        /// </param>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="errorOnUnknownSettings"/> is <c>true</c> and a setting does not map to a
        /// <see cref="UiConfig"/> property.
        /// </exception>
        public static UiConfig LoadEffectiveConfig(string environment, bool errorOnUnknownSettings = false)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(LoadBaseSettings())
                .AddInMemoryCollection(LoadProfileSettings(environment))
                .Build();

            var config = configuration.Get<UiConfig>(options =>
                options.ErrorOnUnknownConfiguration = errorOnUnknownSettings);

            return config ?? new UiConfig();
        }

        /// <summary>
        /// Gets the name of the profile file for the specified environment.
        /// </summary>
        public static string GetProfileFileName(string environment)
        {
            return $"{ProfileFilePrefix}{environment}{FileExtension}";
        }

        private static Dictionary<string, string> LoadSettings(string fileName)
        {
            var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var stream = new YamlStream();

            using (var reader = new StreamReader(Path.Combine(AppContext.BaseDirectory, fileName)))
            {
                stream.Load(reader);
            }

            foreach (var document in stream.Documents)
            {
                Flatten(document.RootNode, null, settings);
            }

            return settings;
        }

        private static void Flatten(YamlNode node, string path, Dictionary<string, string> settings)
        {
            switch (node)
            {
                case YamlMappingNode mapping:
                    foreach (var (key, value) in mapping.Children)
                    {
                        var name = ((YamlScalarNode)key).Value;
                        Flatten(value, path is null ? name : ConfigurationPath.Combine(path, name), settings);
                    }

                    break;
                case YamlSequenceNode sequence when path is not null:
                    for (var index = 0; index < sequence.Children.Count; index++)
                    {
                        var key = ConfigurationPath.Combine(path, index.ToString(CultureInfo.InvariantCulture));
                        Flatten(sequence.Children[index], key, settings);
                    }

                    break;
                case YamlScalarNode scalar when path is not null:
                    settings[path] = scalar.Value;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported YAML node at '{path}': {node.NodeType}.");
            }
        }
    }
}
