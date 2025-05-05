namespace MikeNakis.SvgConvert;

using MikeNakis.Kit.Collections;
using MikeNakis.Console;
using MikeNakis.Kit.FileSystem;
using MikeNakis.Kit;
using MikeNakis.Kit.Extensions;
using MikeNakis.Clio.Extensions;
using SysImage = SysDraw.Imaging;

sealed class Svg2IcoMain
{
	static void Main( string[] arguments )
	{
		ConsoleHelpers.Run( false, () => run( arguments ) );
	}

	static int run( string[] arguments )
	{
		Clio.ArgumentParser argumentParser = new();
		Clio.IVerbArgument watchVerb = argumentParser.AddVerb( "png", description: "Converts to PNG", convertToPngHandler );
		Clio.IVerbArgument parseVerb = argumentParser.AddVerb( "windows-ico", description: "Converts to windows-ICO", convertToWindowsIcoHandler );
		if( !argumentParser.TryParse( arguments ) )
			return -1;
		return 0;
	}

	static void convertToPngHandler( Clio.ChildArgumentParser argumentParser )
	{
		Clio.IOptionArgument<string?> widthArgument = argumentParser.AddStringOption( "width", 'w', "The width of the generated PNG file.", "pixels" );
		Clio.IOptionArgument<string?> heightArgument = argumentParser.AddStringOption( "height", 'h', "The height of the generated PNG file.", "pixels" );
		Clio.IPositionalArgument<string> inputFileArgument = argumentParser.AddRequiredStringPositional( "input-file", "The SVG file to read" );
		Clio.IPositionalArgument<string?> outputFileArgument = argumentParser.AddStringPositional( "output-file", "The PNG file to write" );
		if( !argumentParser.TryParse() )
			return;
		FilePath inputFilePath = FilePath.FromRelativeOrAbsolutePath( inputFileArgument.Value );
		FilePath outputFilePath = getPngFilePath( inputFilePath, outputFileArgument.Value );
		Log.Debug( $"Reading {inputFilePath}" );
		Svg.SvgDocument svgDocument = readSvgDocument( inputFilePath );
		int width = widthArgument.Value == null ? (int)svgDocument.Bounds.Width : parseInt( widthArgument.Value );
		int height = heightArgument.Value == null ? (int)svgDocument.Bounds.Height : parseInt( heightArgument.Value );
		Log.Debug( $"Writing {outputFilePath}" );
		SysDraw.Bitmap bitmap = svgDocument.Draw( width, height );
		bitmap.Save( outputFilePath.Path, SysImage.ImageFormat.Png );
		return;

		static FilePath getPngFilePath( FilePath svgFilePath, string? pngFileArgumentValue )
			=> getRelatedFilePath( svgFilePath, pngFileArgumentValue, ".png" );
	}

	static int parseInt( string s )
	{
		//PEARL: When `int.Parse()` fails, it throws a `System.FormatException`.
		//    This absolutely retarded exception contains no fields other than the message,
		//    and the message says nothing but "Input string was not in a correct format."
		//    So, it fails to convey two very important pieces of information:
		//      1. What was the string that was not in a correct format.
		//      2. What was the correct format.
		//    We fix this insanity here.
		try
		{
			return int.Parse( s, SysGlob.CultureInfo.InvariantCulture );
		}
		catch( Sys.Exception exception )
		{
			throw new Sys.InvalidOperationException( $"Failed to parse '{s}' as an integer", exception );
		}
	}

	static void convertToWindowsIcoHandler( Clio.ChildArgumentParser argumentParser )
	{
		Clio.IOptionArgument<string> iconSizesArgument = argumentParser.AddStringOptionWithDefault( "icon-sizes", "16,32,48,64,128,256", 's', "A comma-separated list of icon sizes to create.", "sizes" );
		Clio.IPositionalArgument<string> inputFileArgument = argumentParser.AddRequiredStringPositional( "input-file", "The SVG file to read" );
		Clio.IPositionalArgument<string?> outputFileArgument = argumentParser.AddStringPositional( "output-file", "The windows-ICO file to write" );
		if( !argumentParser.TryParse() )
			return;
		IReadOnlyList<int> iconSizes = iconSizesArgument.Value.Split( ',' ).Select( int.Parse ).Collect();
		FilePath inputFilePath = FilePath.FromRelativeOrAbsolutePath( inputFileArgument.Value );
		FilePath outputFilePath = getIcoFilePath( inputFilePath, outputFileArgument.Value );
		Log.Debug( $"Reading {inputFilePath}" );
		Svg.SvgDocument svgDocument = readSvgDocument( inputFilePath );
		IReadOnlyList<SysDraw.Image> images = createImages( svgDocument, iconSizes );
		SysDraw.Icon icon = iconFromImages( images );
		Log.Debug( $"Writing {outputFilePath}" );
		saveIcon( outputFilePath, icon );
		return;

		static FilePath getIcoFilePath( FilePath svgFilePath, string? icoFileArgumentValue )
			=> getRelatedFilePath( svgFilePath, icoFileArgumentValue, ".ico" );

		static IReadOnlyList<SysDraw.Image> createImages( Svg.SvgDocument svgDocument, IReadOnlyList<int> iconSizes )
		{
			MutableList<SysDraw.Bitmap> bitmaps = new();
			foreach( int iconSize in iconSizes )
			{
				SysDraw.Bitmap bitmap = svgDocument.Draw( iconSize, iconSize );
				bitmaps.Add( bitmap );
			}
			return bitmaps.AsReadOnlyList;
		}

		static void saveIcon( FilePath icoFilePath, SysDraw.Icon icon )
		{
			using( SysIo.FileStream stream = icoFilePath.NewStream( SysIo.FileMode.Create, SysIo.FileAccess.Write ) )
				icon.Save( stream );
		}

		static SysDraw.Icon iconFromImages( IReadOnlyList<SysDraw.Image> images )
		{
			// See https://en.wikipedia.org/wiki/ICO_(file_format)
			const int iconDirStructureSize = 6;
			const int iconDirEntryStructureSize = 16;
			using SysIo.MemoryStream memoryStream = new();
			using SysIo.BinaryWriter writer = new( memoryStream );
			writer.Write( (short)0 );   // 0 : reserved
			writer.Write( (short)1 );   // 2 : 1=ico, 2=cur
			writer.Write( (short)images.Count ); // 4 : number of images
			Assert( memoryStream.Length == iconDirStructureSize );
			SysIo.MemoryStream[] memoryStreams = new SysIo.MemoryStream[images.Count];
			int imageDataOffset = iconDirStructureSize + images.Count * iconDirEntryStructureSize;
			for( int i = 0; i < images.Count; i++ )
			{
				SysDraw.Image image = images[i];
				memoryStreams[i] = new();
				image.Save( memoryStreams[i], SysImage.ImageFormat.Png );
				int imageDataLength = (int)memoryStreams[i].Length;
				Assert( memoryStream.Length == iconDirStructureSize + i * iconDirEntryStructureSize );
				writer.Write( (byte)(image.Width >= 256 ? 0 : image.Width) ); // 0 : width of image
				writer.Write( (byte)(image.Height >= 256 ? 0 : image.Height) ); // 1 : height of image
				writer.Write( (byte)0 ); // 2 : number of colors in palette
				writer.Write( (byte)0 ); // 3 : reserved
				writer.Write( (short)0 ); // 4 : number of color planes
				writer.Write( (short)0 ); // 6 : bits per pixel
				writer.Write( imageDataLength ); // 8 : image size
				writer.Write( imageDataOffset ); // 12: offset of image data
				imageDataOffset += imageDataLength;
			}
			foreach( SysDraw.Image image in images )
				image.Save( memoryStream, SysImage.ImageFormat.Png );
			memoryStream.Seek( 0, SysIo.SeekOrigin.Begin );
			return new SysDraw.Icon( memoryStream );
		}
	}

	static Svg.SvgDocument readSvgDocument( FilePath svgFilePath )
	{
		using( SysIo.Stream stream = svgFilePath.NewStream( SysIo.FileMode.Open, SysIo.FileAccess.Read ) )
			return Svg.SvgDocument.Open<Svg.SvgDocument>( stream );
	}

	static FilePath getRelatedFilePath( FilePath sourceFilePath, string? targetFileName, string extension )
	{
		Assert( extension.StartsWith2( "." ) );
		if( targetFileName == null )
			return sourceFilePath.WithReplacedExtension( extension );
		DirectoryPath targetDirectoryPath = DirectoryPath.FromAbsoluteOrRelativePath( targetFileName );
		if( targetFileName.EndsWith( '/' ) || targetFileName.EndsWith( '\\' ) || targetDirectoryPath.Exists() )
			return FilePath.Of( targetDirectoryPath, sourceFilePath.GetFileNameWithoutExtension() + extension );
		return FilePath.FromRelativeOrAbsolutePath( targetFileName );
	}
}
