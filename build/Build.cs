using System;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using static Nuke.Common.Tools.NuGet.NuGetTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

    [Parameter("Configuration to build - Default is 'Release'")]
    readonly Configuration Configuration = Configuration.Release;

    [Solution] readonly Solution Solution;
    [GitRepository] readonly GitRepository GitRepository;

    AbsolutePath SourceDirectory
        => RootDirectory / "src";

    AbsolutePath OutputDirectory
        => RootDirectory / "output";

    AbsolutePath ILRepackBin
        => (AbsolutePath)ToolPathResolver.GetPackageExecutable("ilrepack", "ILRepack.exe");

    Project VirtualBoardProject
        => Solution.GetProject("OpenMacroBoard.VirtualBoard");

    Project OpenMacroBoardSDKProject
        => Solution.GetProject("OpenMacroBoard.SDK");

    Project StreamDeckSharpProject
        => Solution.GetProject("StreamDeckSharp");

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Bleach => _ => _
        .Before(Clean)
        .Before(Restore)
        .Executes(() =>
        {
            RunCodeInRoot("git", "clean -xdf -e /build/bin/ -e /.tmp/build-attempt.log");
            RunCodeInRoot("git", "reset --hard");
            RunCodeInRoot("git", "submodule foreach --recursive \"git clean -xdf\"");
            RunCodeInRoot("git", "submodule foreach --recursive \"git reset --hard\"");
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            MSBuild(s => s
                .SetTargetPath(Solution)
                .SetTargets("Restore"));

            NuGetRestore(new NuGetRestoreSettings()
                .SetTargetPath(Solution)
            );
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            MSBuild(s => s
                .SetTargetPath(Solution)
                .SetTargets("Rebuild")
                .SetConfiguration(Configuration)
                .SetMaxCpuCount(Environment.ProcessorCount)
                .SetNodeReuse(IsLocalBuild));
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Requires(() => Configuration == Configuration.Release)
        .Executes(() =>
        {
            //Compile also packs, we only need to copy the packages to the output location

            var projectDirs = new[]
            {
                VirtualBoardProject.Directory,
                StreamDeckSharpProject.Directory,
                OpenMacroBoardSDKProject.Directory
            };

            foreach (var projectDir in projectDirs)
                MoveNugetPackagesToOutput(projectDir / "bin" / Configuration);
        });

    private void MoveNugetPackagesToOutput(string directory)
    {
        foreach (var file in GlobFiles(directory, "*.nupkg"))
            CopyFile(file, OutputDirectory / Path.GetFileName(file));
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
