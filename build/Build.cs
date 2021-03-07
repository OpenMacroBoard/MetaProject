using Nuke.Common;
using Nuke.Common.Execution;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.MSBuild;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Utilities.Collections;
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using static Nuke.Common.Tools.NuGet.NuGetTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
[SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "It's OK for build scripts")]
class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Compile);

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
        "OpenMacroBoard.VirtualBoard"
    };

    Target UpdateDocs => _ => _
        .Executes(() =>
        {
            CodeSampleUpdater.Run(RootDirectory / "README.md");
        });

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
            foreach (var projectName in ProjectsNames)
            {
                var project = Solution.GetProject(projectName);

                MSBuild(s => s
                    .SetTargetPath(project)
                    .SetTargets("Rebuild")
                    .SetConfiguration(Configuration)
                    .SetMaxCpuCount(Environment.ProcessorCount)
                    .SetNodeReuse(IsLocalBuild)
                );
            }
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Requires(() => Configuration == Configuration.Release)
        .Executes(() =>
        {
            foreach (var projectName in ProjectsNames)
            {
                var project = Solution.GetProject(projectName);
                var version = GetVersion(project);
                var nuspecFile = Path.ChangeExtension(project, ".nuspec");

                NuGetPack(s => s
                    .SetTargetPath(nuspecFile)
                    .SetVersion(version)
                    .SetConfiguration(Configuration)
                    .SetOutputDirectory(OutputDirectory)
                );
            }
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
