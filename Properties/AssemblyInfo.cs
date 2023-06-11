using System.Reflection;
using System.Runtime.InteropServices;
using SearchPlusPlus;
using MelonLoader;

[assembly: MelonInfo(typeof(ModMain), MelonBuildInfo.Name, MelonBuildInfo.Version, MelonBuildInfo.Author, MelonBuildInfo.DownloadLink)]
[assembly: MelonGame("PeroPeroGames", "MuseDash")]

[assembly: AssemblyTitle(MelonBuildInfo.Name)]
[assembly: AssemblyDescription(MelonBuildInfo.Description)]
[assembly: AssemblyProduct(MelonBuildInfo.Name)]
[assembly: MelonIncompatibleAssemblies()]
[assembly: MelonOptionalDependencies()]

[assembly: ComVisible(false)]

//[assembly: AssemblyVersion("0.0.0.0")]
[assembly: AssemblyVersion(MelonBuildInfo.Version)]
[assembly: AssemblyFileVersion(MelonBuildInfo.Version)]