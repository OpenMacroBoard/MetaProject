using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
[SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "It's OK for build scripts")]
class Build : NukeBuild
{
    public static int Main()
    {
        return Execute<Build>(x => x.Pack);
    }

    [Parameter("Configuration to build - Default is 'Release'")]
    readonly Configuration Configuration = Configuration.Release;

    [Solution]
    readonly Solution Solution;

    AbsolutePath SourceDirectory
        => RootDirectory / "src";

    AbsolutePath OutputDirectory
        => RootDirectory / "output";

    readonly string[] ProjectsNames = new string[]
    {
        "StreamDeckSharp",
        "OpenMacroBoard.SDK",
        "OpenMacroBoard.SocketIO",
    };

    Target UpdateDocs => _ => _
        .Executes(() =>
        {
            CodeSampleUpdater.Run(RootDirectory / "README.md");
        });

    Target Clean => _ => _
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Bleach => _ => _
        .Before(Clean)
        .Executes(() =>
        {
            RunCodeInRoot("git", "clean -xdf -e /build/bin/ -e /.tmp/build-attempt.log");
            RunCodeInRoot("git", "reset --hard");
            RunCodeInRoot("git", "submodule foreach --recursive \"git clean -xdf\"");
            RunCodeInRoot("git", "submodule foreach --recursive \"git reset --hard\"");
        });

    Target Pack => _ => _
        .Requires(() => Configuration == Configuration.Release)
        .After(Clean)
        .Executes(() =>
        {
            EnsureExistingDirectory(OutputDirectory);

            foreach (var projectName in ProjectsNames)
            {
                var project = Solution.GetProject(projectName);

                DotNetPack(s => s
                    .SetProject(project)
                    .SetConfiguration(Configuration)
                );

                var binDir = project.Directory / "bin" / Configuration;
                var nupkgFile = GlobFiles(binDir, "*.nupkg").Single();

                MoveDirectoryToDirectory(
                    nupkgFile,
                    OutputDirectory,
                    DirectoryExistsPolicy.Merge,
                    FileExistsPolicy.Overwrite
                );
            }

            var project2 = Solution.GetProject("OpenMacroBoard.VirtualBoard");

            DotNetBuild(s => s
                .SetProjectFile(project2)
                .SetConfiguration(Configuration)
            );

            var releasePath = project2.Directory / "bin" / "Release" / "net6.0-windows";
            GlobFiles(releasePath, "*.xml", "*.pdb", "*.deps.json").ForEach(DeleteFile);

            RenameFile(releasePath / "OpenMacroBoard.VirtualBoard.exe", releasePath / "VirtualBoard.exe");

            ZipFile.CreateFromDirectory(releasePath, OutputDirectory / "VirtualBoard.zip");
        });

    private string GetVersion(string project)
    {
        return XDocument
            .Load(project)
            .Descendants()
            .Where(d => d.Name.LocalName == "PropertyGroup")
            .SelectMany(d => d
                .Descendants()
                .Where(x => x.Name.LocalName == "Version")
            )
            .FirstOrDefault()
            ?.Value;
    }

    private void RunCodeInRoot(string toolPath, string arguments)
    {
        var proc = ProcessTasks.StartProcess(toolPath,
                workingDirectory: RootDirectory,
                arguments: arguments
            );

        proc.WaitForExit();
        proc.AssertZeroExitCode();
    }
}
