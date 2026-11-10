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
        public static string ApiUrl { get; }

        public static RestClient RestClient { get; }

        public static string[] SteeltoeVersions { get; }

        public static string[] DotNetFrameworks { get; }

        public static string[] Dependencies { get; }

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
                ApiUrl = GetApiUrl(settings);
            }

            using (var reader = new StreamReader("SteeltoeInitializr.yaml"))
            {
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(CamelCaseNamingConvention.Instance)
                    .Build();
                var config = deserializer.Deserialize<UiConfig>(reader);
                SteeltoeVersions = GetSteeltoeVersions(config);
                DotNetFrameworks = GetDotNetFrameworks(config);
                Dependencies = GetDependencies(config);
            }

            RestClient = new RestClient(ApiUrl);
        }

        private static string GetApiUrl(TestSettings settings)
        {
            return Environment.GetEnvironmentVariable("INITIALIZR_API_URL") ?? settings.ApiUrl;
        }

        private static string[] GetSteeltoeVersions(UiConfig config)
        {
            var versions = new List<string>();
            foreach (var version in config.SteeltoeVersion.Values)
            {
                versions.Add(version.Id);
            }

            return versions.ToArray();
        }

        private static string[] GetDotNetFrameworks(UiConfig config)
        {
            var frameworks = new List<string>();
            foreach (var framework in config.DotNetFramework.Values)
            {
                frameworks.Add(framework.Id);
            }

            return frameworks.ToArray();
        }

        private static string[] GetDependencies(UiConfig config)
        {
            var deps = new List<string>();
            foreach (var group in config.Dependencies.Values)
            {
                foreach (var item in group.Values)
                {
                    deps.Add(item.Id);
                }
            }

            return deps.ToArray();
        }
    }

    class TestSettings
    {
        public string ApiUrl { get; set; }
    }
}
