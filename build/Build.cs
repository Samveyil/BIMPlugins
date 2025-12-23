using BIMPlugins.Nuke;
using Nuke.Common;
using Nuke.Common.ProjectModel;

public class Build : BIMPluginsBuild
{
    protected override int MajorVersion => 3;
    protected override int MinorVersion => 0;
    protected override int MaintenanceVersion => 2;


    [Solution("BIMPlugins.sln")]
    public override Solution Solution { get; }

    [Parameter("Project to build name")]
    public override string ProjectName { get; } = "BIMPlugins";

    [Parameter("Build configurations to run sequentially")]
    public override Configuration[] Configurations { get; } = new[] { Configuration.R2019, Configuration.R2020, Configuration.R2021, Configuration.R2022, Configuration.R2023 };

    public static int Main() => Execute<Build>(x => x.Compile);
}