using BIMPlugins.Nuke;
using Nuke.Common;
using Nuke.Common.ProjectModel;

public class Build : BIMPluginsBuild
{
    protected override int MajorVersion => 3;
    protected override int MinorVersion => 2;
    protected override int MaintenanceVersion => 1;


    [Solution("BIMPlugins.sln")]
    protected override Solution Solution { get; }

    [Parameter("Name of the project to build")]
    protected override string ProjectName { get; } = "BIMPlugins";

    [Parameter("Configurations to build")]
    protected override string[] Configurations { get; } = new[] { "R2019", "R2020", "R2021", "R2022", "R2023" };

    public static int Main() => Execute<Build>(x => x.Compile);
}