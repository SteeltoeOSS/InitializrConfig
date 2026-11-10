using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using FluentAssertions;
using RestSharp;
using Xunit;

namespace Steeltoe.Initializr.ApiTests
{
    public class ProjectTests(ITestOutputHelper output)
    {
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
                output.WriteLine("skipping unsupported combination");
                return;
            }

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.IsSuccessful.Should().BeTrue();
            output.WriteLine($"working directory: {workingDirectory}");
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, true);
            }

            Directory.CreateDirectory(workingDirectory);
            using (var buf = new MemoryStream(response.RawBytes ?? []))
            {
                var archive = new ZipArchive(buf);
                foreach (var entry in archive.Entries)
                {
                    var path = Path.GetFullPath(Path.Combine(workingDirectory, entry.FullName));
                    if (path.EndsWith(Path.DirectorySeparatorChar))
                    {
                        output.WriteLine($"creating directory {path}");
                        Directory.CreateDirectory(path);
                    }
                    else
                    {
                        output.WriteLine($"extracting {path}");
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
                output.WriteLine("--- process STDOUT ---");
                output.WriteLine(proc.StandardOutput.ReadToEnd());
                output.WriteLine("--- process STDERR ---");
                output.WriteLine(proc.StandardError.ReadToEnd());
                proc.ExitCode.Should().Be(0);
            }

            Directory.Delete(workingDirectory, true);
        }
    }
}
