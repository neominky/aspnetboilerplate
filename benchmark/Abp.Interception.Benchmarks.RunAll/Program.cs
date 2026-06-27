using System.Diagnostics;
using System.Text;

namespace Abp.Interception.Benchmarks.RunAll;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var solutionRoot = FindSolutionRoot();
        var configuration = "Release";
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is "-c" or "--configuration")
            {
                configuration = args[i + 1];
                break;
            }
        }

        var projects = new[]
        {
            ("NuGet Abp 10.4 (Castle DynamicProxy)", Path.Combine(solutionRoot, "benchmark", "Abp.Interception.Benchmarks.NuGet", "Abp.Interception.Benchmarks.NuGet.csproj")),
            ("Fork compile-time vs allocation-free", Path.Combine(solutionRoot, "benchmark", "Abp.Interception.Benchmarks.Fork", "Abp.Interception.Benchmarks.Fork.csproj")),
        };

        Console.WriteLine("Running interceptor benchmarks (separate processes — NuGet Abp and fork Abp cannot load in one process).");
        Console.WriteLine();

        var exitCode = 0;
        foreach (var (title, projectPath) in projects)
        {
            Console.WriteLine(new string('=', 80));
            Console.WriteLine(title);
            Console.WriteLine(new string('=', 80));

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --configuration {configuration} --project \"{projectPath}\" --",
                WorkingDirectory = solutionRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(processStartInfo);
            if (process == null)
            {
                Console.Error.WriteLine($"Failed to start benchmark: {projectPath}");
                exitCode = 1;
                continue;
            }

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stdout.AppendLine(e.Data);
                    Console.WriteLine(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    stderr.AppendLine(e.Data);
                    Console.Error.WriteLine(e.Data);
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                exitCode = process.ExitCode;
            }

            Console.WriteLine();
        }

        return exitCode;
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Abp.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root (Abp.sln).");
    }
}
