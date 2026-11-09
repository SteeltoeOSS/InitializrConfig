using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RestSharp;
using Steeltoe.InitializrService.Config;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Steeltoe.Initializr.ApiTests
{
    public static class Configuration
    {
        public static string ApiUrl { get; private set; }

        public static RestClient RestClient { get; private set; }

        public static string[] SteeltoeVersions { get; private set; }

        public static string[] DotNetFrameworks { get; private set; }

        public static string[] Dependencies { get; private set; }

        public static string GetTestDir(string version, string framework, string dependency)
        {
            return Path.Combine(TestsDir, $"{version}--{framework}--{dependency}");
        }

        private static string TestsDir { get; } =
            Path.Join(Path.GetTempPath(), Assembly.GetExecutingAssembly().GetName().Name);

        static Configuration()
        {
            using (var reader = new StreamReader("test-settings.yaml"))
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var settings = deserializer.Deserialize<TestSettings>(reader);
                InitializeApiUrl(settings);
            }

            using (var reader = new StreamReader("SteeltoeInitializr.yaml"))
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var config = deserializer.Deserialize<UiConfig>(reader);
                InitializeSteeltoeVersions(config);
                InitializeDotNetFrameworks(config);
                InitializeDependencies(config);
            }

            RestClient = new RestClient(ApiUrl);
        }

        private static void InitializeApiUrl(TestSettings settings)
        {
            ApiUrl = Environment.GetEnvironmentVariable("INITIALIZR_API_URL") ?? settings.ApiUrl;
        }

        private static void InitializeSteeltoeVersions(UiConfig config)
        {
            var versions = new List<string>();
            foreach (var version in config.SteeltoeVersion.Values)
            {
                versions.Add(version.Id);
            }

            SteeltoeVersions = versions.ToArray();
        }

        private static void InitializeDotNetFrameworks(UiConfig config)
        {
            var frameworks = new List<string>();
            foreach (var framework in config.DotNetFramework.Values)
            {
                frameworks.Add(framework.Id);
            }

            DotNetFrameworks = frameworks.ToArray();
        }

        private static void InitializeDependencies(UiConfig config)
        {
            var deps = new List<string>();
            foreach (var group in config.Dependencies.Values)
            {
                foreach (var item in group.Values)
                {
                    deps.Add(item.Id);
                }
            }

            Dependencies = deps.ToArray();
        }
    }

    class TestSettings
    {
        public string ApiUrl { get; set; }
    }
}
