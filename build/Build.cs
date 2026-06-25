using BIMPlugins.Nuke;
using Nuke.Common;
using Nuke.Common.ProjectModel;

public class Build : BIMPluginsBuild
{
    protected override int MajorVersion => 3;
    protected override int MinorVersion => 1;
    protected override int MaintenanceVersion => 4;


    [Solution("BIMPlugins.sln")]
    protected override Solution Solution { get; }

    [Parameter("Имя проекта для сборки")]
    protected override string ProjectName { get; } = "BIMPlugins";

    public static int Main() => Execute<Build>(x => x.Compile);
}