#pragma warning disable IDE0001 //Simplify name
#pragma warning disable IDE0002 //Simplify member access
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
