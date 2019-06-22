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

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

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
        .Executes(() =>
        {
            if (Configuration != Configuration.Release)
                throw new NotSupportedException("Configuration must be 'Release' to create nuget packages.");

            var streamDeckSharpOutput = StreamDeckSharpProject.Directory / "bin" / Configuration;
            var merged = streamDeckSharpOutput / "Merged";
            var outDll = merged / "StreamDeckSharp.dll";

            var streamDeckDll = streamDeckSharpOutput / "StreamDeckSharp.dll";
            var hidLibrary = streamDeckSharpOutput / "HidLibrary.dll";

            EnsureCleanDirectory(merged);
            var args = $"/out:{outDll.Quoted()} /xmldocs /internalize {streamDeckDll.Quoted()} {hidLibrary.Quoted()}";

            var proc = ProcessTasks.StartProcess(ILRepackBin,
                workingDirectory: streamDeckSharpOutput,
                arguments: args
            );

            proc.WaitForExit();
            proc.AssertZeroExitCode();

            NuGetPack(s => s
                .SetTargetPath(Path.ChangeExtension(VirtualBoardProject.Path, "nuspec"))
                .SetOutputDirectory(OutputDirectory)
            );

            NuGetPack(s => s
                .SetTargetPath(Path.ChangeExtension(OpenMacroBoardSDKProject.Path, "nuspec"))
                .SetOutputDirectory(OutputDirectory)
            );

            NuGetPack(s => s
                .SetTargetPath(Path.ChangeExtension(StreamDeckSharpProject.Path, "nuspec"))
                .SetOutputDirectory(OutputDirectory)
            );
        });
}
