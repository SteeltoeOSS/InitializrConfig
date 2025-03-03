using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using FluentAssertions;
using RestSharp;
using Xunit;
using Xunit.Abstractions;

namespace Steeltoe.Initializr.ApiTests
{
    public class ProjectTests
    {
        private readonly ITestOutputHelper _console;

        public ProjectTests(ITestOutputHelper output)
        {
            _console = output;
        }

        [Theory]
        [ClassData(typeof(AllProjectCombinationsWithDependencies))]
        public void TestProjectCombinationWithDependency(string version, string framework, string dependency)
        {
            var request = new RestRequest("project")
                .AddParameter("steeltoeVersion", version)
                .AddParameter("dotNetFramework", framework)
                .AddParameter("dependencies", dependency);
            var testDir = Configuration.GetTestDir(version, framework, dependency);
            DownloadAndBuildProject(request, testDir);
        }

        [Theory]
        [ClassData(typeof(AllProjectCombinations))]
        public void TestProjectCombinationWithAllDependencies(string version, string framework)
        {
            var deps = string.Join(',', Configuration.Dependencies);
            var request = new RestRequest("project")
                .AddParameter("steeltoeVersion", version)
                .AddParameter("dotNetFramework", framework)
                .AddParameter("dependencies", deps);

            var testDir = Configuration.GetTestDir(version, framework, "ALL");
            DownloadAndBuildProject(request, testDir);
        }

        private void DownloadAndBuildProject(RestRequest request, string workingDirectory)
        {
            var response = Configuration.RestClient.Get(request);
            if (response.StatusCode.Equals(HttpStatusCode.NotFound))
            {
                _console.WriteLine($"skipping unsupported combination");
                return;
            }

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.IsSuccessful.Should().BeTrue();
            _console.WriteLine($"working directory: {workingDirectory}");
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, true);
            }

            Directory.CreateDirectory(workingDirectory);
            using (var buf = new MemoryStream(response.RawBytes))
            {
                var archive = new ZipArchive(buf);
                foreach (var entry in archive.Entries)
                {
                    var path = Path.GetFullPath(Path.Combine(workingDirectory, entry.FullName));
                    if (path.EndsWith(Path.DirectorySeparatorChar))
                    {
                        _console.WriteLine($"creating directory {path}");
                        Directory.CreateDirectory(path);
                    }
                    else
                    {
                        _console.WriteLine($"extracting {path}");
                        entry.ExtractToFile(path);
                    }
                }
            }

            var pInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList = {"build"},
                WorkingDirectory = Path.Combine(workingDirectory, "Sample"),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            var proc = Process.Start(pInfo);
            Assert.NotNull(proc);
            proc.WaitForExit();
            if (proc.ExitCode != 0)
            {
                _console.WriteLine("--- process STDOUT ---");
                _console.WriteLine(proc.StandardOutput.ReadToEnd());
                _console.WriteLine("--- process STDERR ---");
                _console.WriteLine(proc.StandardError.ReadToEnd());
                proc.ExitCode.Should().Be(0);
            }

            Directory.Delete(workingDirectory, true);
        }
    }
}
