namespace MikeNakis.SvgConvert;

using System.Collections.Generic;
using System.Linq;
using MikeNakis.Clio.Extensions;
using Sk = SkiaSharp;
using Svg = Svg.Skia;
using Sys = System;
using SysGlob = System.Globalization;
using SysIo = System.IO;

// One command-line to try debugging:
// windows-ico ..\..\Solution\WowsIntel\WowsIntel.ico.svg ..\..\Solution\WowsIntel\obj\WowsIntel.ico --sizes=16,32,48,256

sealed class Svg2IcoMain
{
	static int Main( string[] arguments )
	{
		try
		{
			return run( arguments );
		}
		catch( Sys.Exception exception )
		{
			Sys.Console.Error.WriteLine( $"ERROR: {exception}" );
			return -1;
		}
	}

	static int run( string[] arguments )
	{
		Clio.ArgumentParser argumentParser = new();
		Clio.IVerbArgument watchVerb = argumentParser.AddVerb( "png", description: "Converts to PNG", argumentParser =>
		{
			Clio.IOptionArgument<string?> widthArgument = argumentParser.AddStringOption( "width", 'w', "The width of the generated PNG file.", "pixels" );
			Clio.IOptionArgument<string?> heightArgument = argumentParser.AddStringOption( "height", 'h', "The height of the generated PNG file.", "pixels" );
			Clio.IPositionalArgument<string> inputFileArgument = argumentParser.AddRequiredStringPositional( "input-file", "The SVG file to read" );
			Clio.IPositionalArgument<string?> outputFileArgument = argumentParser.AddStringPositional( "output-file", "The PNG file to write" );
			if( !argumentParser.TryParse() )
				return;
			string inputFilePath = SysIo.Path.GetFullPath( inputFileArgument.Value );
			string outputFilePath = getRelatedFilePath( inputFilePath, outputFileArgument.Value, ".png" );
			int? width = widthArgument.Value == null ? null : parseInt( widthArgument.Value );
			int? height = heightArgument.Value == null ? null : parseInt( heightArgument.Value );
			convertToPng( inputFilePath, outputFilePath, width, height );
		} );
		Clio.IVerbArgument parseVerb = argumentParser.AddVerb( "windows-ico", description: "Converts to windows-ICO", argumentParser =>
		{
			Clio.IOptionArgument<string> iconSizesArgument = argumentParser.AddStringOptionWithDefault( "sizes", "16,32,48,64,128,256", 's', "A comma-separated list of icon sizes to create.", "sizes" );
			Clio.IPositionalArgument<string> inputFileArgument = argumentParser.AddRequiredStringPositional( "input-file", "The SVG file to read" );
			Clio.IPositionalArgument<string?> outputFileArgument = argumentParser.AddStringPositional( "output-file", "The windows-ICO file to write" );
			if( !argumentParser.TryParse() )
				return;
			IReadOnlyList<int> iconSizes = iconSizesArgument.Value.Split( ',' ).Select( int.Parse ).ToList();
			string inputFilePath = SysIo.Path.GetFullPath( inputFileArgument.Value );
			string outputFilePath = getRelatedFilePath( inputFilePath, outputFileArgument.Value, ".ico" );
			convertToWindowsIco( inputFilePath, outputFilePath, iconSizes );
		} );
		if( !argumentParser.TryParse( arguments ) )
			return -1;
		return 0;
	}

	static Svg.SKSvg loadSvg( string inputFilePath )
	{
		var svg = new Svg.SKSvg();
		try
		{
			using( SysIo.FileStream stream = SysIo.File.OpenRead( inputFilePath ) )
				if( svg.Load( stream ) == null )
					throw new Sys.InvalidOperationException( "Svg.Skia sucks" );
		}
		catch( Sys.Exception )
		{
			svg.Dispose();
			throw;
		}
		return svg;
	}

	const double aspectRatioTolerance = 1e-15;

	static (int width, int height) getWidthAndHeight( Sk.SKPicture svgPicture, int? maybeWidth, int? maybeHeight )
	{
		int width = (int)svgPicture.CullRect.Width;
		int height = (int)svgPicture.CullRect.Height;
		double aspectRatio = (double)width / height;
		if( maybeWidth.HasValue && maybeHeight.HasValue )
		{
			double requestedAspectRatio = (double)maybeWidth.Value / maybeHeight.Value;
			if( Sys.Math.Abs( aspectRatio - requestedAspectRatio ) >= aspectRatioTolerance )
				throw new Sys.InvalidOperationException( $"SVG aspect ratio is {aspectRatio}, requested width {maybeWidth.Value} and height {maybeHeight.Value} have a different aspect ratio of {requestedAspectRatio}." );
			width = maybeWidth.Value;
			height = maybeHeight.Value;
		}
		else if( maybeWidth.HasValue )
		{
			width = maybeWidth.Value;
			height = (int)(width / aspectRatio);
		}
		else if( maybeHeight.HasValue )
		{
			height = maybeHeight.Value;
			width = (int)(height * aspectRatio);
		}
		return (width, height);
	}

	static void convertToPng( string inputFilePath, string outputFilePath, int? maybeWidth, int? maybeHeight )
	{
		if( !hasExtension( inputFilePath, ".svg" ) )
			throw new Sys.InvalidOperationException( "input filename does not have an .svg extension" );
		if( !hasExtension( outputFilePath, ".png" ) )
			throw new Sys.InvalidOperationException( "output filename does not have an .png extension" );
		Sys.Console.WriteLine( $"Reading {inputFilePath}" );
		using( Svg.SKSvg svg = loadSvg( inputFilePath ) )
		{
			Sk.SKPicture svgPicture = svg.Picture ?? throw new Sys.InvalidOperationException( "Svg.Skia sucks" );
			(int width, int height) = getWidthAndHeight( svgPicture, maybeWidth, maybeHeight );
			generatePngData( svgPicture, width, height, data =>
			{
				using( SysIo.FileStream stream = SysIo.File.Create( outputFilePath ) )
					data.SaveTo( stream );
			} );
		}
		return;
	}

	static void convertToWindowsIco( string inputFilePath, string outputFilePath, IReadOnlyList<int> iconSizes )
	{
		if( !hasExtension( inputFilePath, ".svg" ) )
			throw new Sys.InvalidOperationException( "input filename does not have an .svg extension" );
		if( !hasExtension( outputFilePath, ".ico" ) )
			throw new Sys.InvalidOperationException( "output filename does not have an .ico extension" );
		using( Svg.SKSvg svg = loadSvg( inputFilePath ) )
		{
			Sk.SKPicture svgPicture = svg.Picture ?? throw new Sys.InvalidOperationException( "Svg.Skia sucks" );
			if( (int)svgPicture.CullRect.Width != (int)svgPicture.CullRect.Height )
				throw new Sys.InvalidOperationException( "SVG image is not rectangular." );
			byte[][] imageDataArrays = new byte[iconSizes.Count][];
			for( int i = 0; i < iconSizes.Count; i++ )
			{
				int width = iconSizes[i];
				int height = iconSizes[i];
				generatePngData( svgPicture, width, height, data =>
				{
					SysIo.MemoryStream memoryStream = new();
					data.SaveTo( memoryStream );
					imageDataArrays[i] = memoryStream.ToArray();
				} );
			}

			Sys.Console.WriteLine( $"Writing {outputFilePath}" );
			using( SysIo.FileStream stream = SysIo.File.Create( outputFilePath ) )
			{
				// See https://en.wikipedia.org/wiki/ICO_(file_format)
				const int iconDirStructureSize = 6;
				const int iconDirEntryStructureSize = 16;
				using SysIo.BinaryWriter writer = new( stream );
				writer.Write( (short)0 );   // 0 : reserved
				writer.Write( (short)1 );   // 2 : 1=ico, 2=cur
				writer.Write( (short)iconSizes.Count ); // 4 : number of images
				assert( stream.Length == iconDirStructureSize );
				int imageDataOffset = iconDirStructureSize + iconSizes.Count * iconDirEntryStructureSize;
				for( int i = 0; i < iconSizes.Count; i++ )
				{
					int width = iconSizes[i];
					int height = iconSizes[i];
					int imageDataLength = imageDataArrays[i].Length;
					assert( stream.Length == iconDirStructureSize + i * iconDirEntryStructureSize );
					writer.Write( (byte)(width >= 256 ? 0 : width) ); // 0 : width of image
					writer.Write( (byte)(height >= 256 ? 0 : height) ); // 1 : height of image
					writer.Write( (byte)0 ); // 2 : number of colors in palette
					writer.Write( (byte)0 ); // 3 : reserved
					writer.Write( (short)0 ); // 4 : number of color planes
					writer.Write( (short)0 ); // 6 : bits per pixel
					writer.Write( imageDataLength ); // 8 : image size
					writer.Write( imageDataOffset ); // 12: offset of image data
					imageDataOffset += imageDataLength;
				}
				for( int i = 0; i < iconSizes.Count; i++ )
					stream.Write( imageDataArrays[i] );
			}
		}
		return;
	}

	static void generatePngData( Sk.SKPicture svgPicture, int width, int height, Sys.Action<Sk.SKData> handler )
	{
		Sk.SKImageInfo imageInfo = new Sk.SKImageInfo( width, height );
		using( Sk.SKSurface surface = Sk.SKSurface.Create( imageInfo ) )
		using( Sk.SKCanvas canvas = surface.Canvas )
		{
			Sk.SKMatrix matrix = Sk.SKMatrix.CreateScale( width / svgPicture.CullRect.Width, height / svgPicture.CullRect.Height );
			canvas.Clear( Sk.SKColors.Transparent );
			canvas.DrawPicture( svgPicture, in matrix );
			canvas.Flush();

			using( Sk.SKImage image = surface.Snapshot() )
			using( Sk.SKData data = image.Encode( Sk.SKEncodedImageFormat.Png, 100 ) )
			{
				handler.Invoke( data );
			}
		}
	}

	static string getRelatedFilePath( string sourceFilePath, string? targetFileName, string extension )
	{
		assert( extension.StartsWith( '.' ) );
		if( targetFileName == null )
			return SysIo.Path.ChangeExtension( sourceFilePath, extension );
		string targetPath = SysIo.Path.GetFullPath( targetFileName );
		if( targetFileName.EndsWith( '/' ) || targetFileName.EndsWith( '\\' ) || SysIo.Directory.Exists( targetPath ) )
			return SysIo.Path.Combine( targetPath, SysIo.Path.GetFileNameWithoutExtension( sourceFilePath ) + extension );
		return targetPath;
	}

	static bool hasExtension( string filePath, string extension )
	{
		return string.Equals( SysIo.Path.GetExtension( filePath ), extension, Sys.StringComparison.OrdinalIgnoreCase );
	}

	static void assert( bool condition )
	{
		if( !condition )
			throw new Sys.InvalidOperationException( "Assertion failed" );
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
}
