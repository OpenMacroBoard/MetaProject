using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using System.IO.Compression;

using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

#pragma warning disable S1144   // Unused private types or members should be removed
#pragma warning disable S3903   // Types should be defined in named namespaces
#pragma warning disable IDE0051 // Remove unused private members

class Build : NukeBuild
{
    public static int Main()
    {
        return Execute<Build>(x => x.Pack);
    }

    [Parameter("Configuration to build - Default is 'Release'")]
    readonly Configuration Configuration = Configuration.Release;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    [Solution]
    readonly Solution Solution;
#pragma warning restore CS8618

    AbsolutePath SourceDirectory
        => RootDirectory / "src";

    AbsolutePath OutputDirectory
        => RootDirectory / "output";

    readonly string[] ProjectsNames =
    [
        "StreamDeckSharp",
        "OpenMacroBoard.SDK",
        "OpenMacroBoard.SocketIO",
    ];

    Target UpdateDocs => _ => _
        .Executes(() =>
        {
            CodeSampleUpdater.Run(RootDirectory / "README.md");
        });

    Target Clean => _ => _
        .Executes(() =>
        {
            SourceDirectory
                .GlobDirectories("**/bin", "**/obj")
                .ForEach(x => x.DeleteDirectory());

            OutputDirectory.CreateOrCleanDirectory();
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
            OutputDirectory.CreateOrCleanDirectory();

            foreach (var projectName in ProjectsNames)
            {
                var project = Solution.GetProject(projectName);

                DotNetPack(s => s
                    .SetProject(project)
                    .SetConfiguration(Configuration)
                );

                var binDir = project.Directory / "bin" / Configuration;
                var nupkgFile = binDir.GlobFiles("*.nupkg").Single();

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

            releasePath
                .GlobFiles("*.xml", "*.pdb", "*.deps.json")
                .ForEach(f => f.DeleteFile());

            RenameFile(releasePath / "OpenMacroBoard.VirtualBoard.exe", releasePath / "VirtualBoard.exe");

            ZipFile.CreateFromDirectory(releasePath, OutputDirectory / "VirtualBoard.zip");
        });

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
