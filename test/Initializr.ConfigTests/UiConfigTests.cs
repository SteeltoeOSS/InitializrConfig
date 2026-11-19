// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the Apache 2.0 License.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using Steeltoe.InitializrService.Config;
using Xunit;

namespace Steeltoe.Initializr.ConfigTests
{
    /// <summary>
    /// Validates the effective configuration that InitializrService serves for each environment.
    /// </summary>
    public class UiConfigTests
    {
        public static TheoryData<string> Environments { get; } = new(UiConfigLoader.GetEnvironments());

        [Fact]
        public void EnvironmentsAreDiscovered()
        {
            Environments.Should().NotBeEmpty();
        }

        [Theory]
        [MemberData(nameof(Environments))]
        public void ContainsOnlyKnownSettings(string environment)
        {
            var load = () => UiConfigLoader.LoadEffectiveConfig(environment, errorOnUnknownSettings: true);

            load.Should().NotThrow();
        }

        [Theory]
        [MemberData(nameof(Environments))]
        public void TextFieldsHaveDefaults(string environment)
        {
            var config = UiConfigLoader.LoadEffectiveConfig(environment);

            using (new AssertionScope())
            {
                AssertTextHasDefault(config.Name, "name");
                AssertTextHasDefault(config.Namespace, "namespace");
                AssertTextHasDefault(config.Description, "description");
            }
        }

        [Theory]
        [MemberData(nameof(Environments))]
        public void SingleSelectListsHaveValidDefaults(string environment)
        {
            var config = UiConfigLoader.LoadEffectiveConfig(environment);

            using (new AssertionScope())
            {
                AssertValidSingleSelectList(config.SteeltoeVersion, "steeltoeVersion");
                AssertValidSingleSelectList(config.DotNetFramework, "dotNetFramework");
                AssertValidSingleSelectList(config.Language, "language");
                AssertValidSingleSelectList(config.Packaging, "packaging");
            }
        }

        [Theory]
        [MemberData(nameof(Environments))]
        public void DependenciesAreValid(string environment)
        {
            var config = UiConfigLoader.LoadEffectiveConfig(environment);
            var groups = config.Dependencies.Values;
            var items = groups.SelectMany(group => group.Values ?? []).ToArray();

            using (new AssertionScope())
            {
                groups.Should().NotBeEmpty("at least one dependency group must be configured");

                foreach (var group in groups)
                {
                    group.Name.Should().NotBeNullOrWhiteSpace("every dependency group must have a name");
                    group.Values.Should().NotBeNullOrEmpty($"dependency group '{group.Name}' must have values");
                }

                foreach (var item in items)
                {
                    item.Id.Should().NotBeNullOrWhiteSpace("every dependency must have an ID");
                    item.Name.Should().NotBeNullOrWhiteSpace($"dependency '{item.Id}' must have a name");
                }

                // InitializrService matches requested dependencies to IDs case-insensitively.
                var duplicateIds = items
                    .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                    .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
                    .Where(grouping => grouping.Count() > 1)
                    .Select(grouping => grouping.Key);

                duplicateIds.Should().BeEmpty("dependency IDs must be unique across all groups");

                if (!string.IsNullOrEmpty(config.Dependencies.Default))
                {
                    var ids = items.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

                    config.Dependencies.Default.Split(',').Should().OnlyContain(id => ids.Contains(id),
                        "default dependencies must refer to configured dependencies");
                }
            }
        }

        [Theory]
        [MemberData(nameof(Environments))]
        public void ProfileListsDoNotInheritBaseEntries(string environment)
        {
            var baseSettings = UiConfigLoader.LoadBaseSettings();
            var profileSettings = UiConfigLoader.LoadProfileSettings(environment);
            var leakedKeys = new List<string>();

            foreach (var key in baseSettings.Keys.Where(key => !profileSettings.ContainsKey(key)))
            {
                // If the profile overrides a list, every base setting inside that list must be overridden too.
                // Otherwise, Config Server merges leftover base entries or properties into the profile's list.
                foreach (var listPath in GetListPaths(key))
                {
                    if (profileSettings.Keys.Any(profileKey =>
                        profileKey.StartsWith(listPath + ':', StringComparison.OrdinalIgnoreCase)))
                    {
                        leakedKeys.Add(key);
                        break;
                    }
                }
            }

            var profileFileName = UiConfigLoader.GetProfileFileName(environment);

            leakedKeys.Should().BeEmpty(
                $"{profileFileName} overrides lists from {UiConfigLoader.BaseFileName}, so it must also override " +
                "these settings to prevent them from being merged into its lists");
        }

        private static void AssertTextHasDefault(UiConfig.Text text, string name)
        {
            text.Should().NotBeNull($"{name} must be configured");
            text?.Default.Should().NotBeNullOrWhiteSpace($"{name} must have a default");
        }

        private static void AssertValidSingleSelectList(UiConfig.SingleSelectList list, string name)
        {
            list.Should().NotBeNull($"{name} must be configured");

            if (list is null)
            {
                return;
            }

            list.Values.Should().NotBeNullOrEmpty($"{name} must have values");
            var ids = (list.Values ?? []).Select(item => item.Id).ToArray();

            ids.Should().NotContain(id => string.IsNullOrWhiteSpace(id), $"every {name} value must have an ID");
            ids.Should().OnlyHaveUniqueItems($"{name} IDs must be unique");
            list.Default.Should().BeOneOf(ids, $"the {name} default must be one of its values");
        }

        private static IEnumerable<string> GetListPaths(string key)
        {
            var segments = key.Split(':');

            for (var index = 1; index < segments.Length; index++)
            {
                if (int.TryParse(segments[index], out _))
                {
                    yield return string.Join(':', segments[..index]);
                }
            }
        }
    }
}
