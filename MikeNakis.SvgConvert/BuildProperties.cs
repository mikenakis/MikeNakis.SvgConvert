#pragma warning disable IDE0001 //Name can be simplified
#pragma warning disable IDE0055 //Fix formatting
#pragma warning disable IDE0130 //Namespace does not match folder structure
namespace MikeNakis.SvgConvert;

static class BuildProperties
{
#if RELEASE
	//do nothing
#else
	[System.Runtime.CompilerServices.ModuleInitializer]
	public static void Initialize() => MikeNakis.Kit.Log.SetStartupProjectDirectoryPathName( @"C:\Users\MBV\Personal\Dev\Dotnet\Main\MikeNakis.SvgConvert\MikeNakis.SvgConvert" );
#endif
}
