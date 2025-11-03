namespace MikeNakis.SvgConvert;

using System.Collections.Generic;
using System.Linq;
using MikeNakis.Clio.Extensions;
using MikeNakis.Console;
using MikeNakis.Kit;
using MikeNakis.Kit.Collections;
using MikeNakis.Kit.Extensions;
using MikeNakis.Kit.FileSystem;
using Sk = SkiaSharp;
using Svg = Svg.Skia;
using Sys = System;
using SysGlob = System.Globalization;
using SysIo = System.IO;

sealed class Svg2IcoMain
{
	static void Main( string[] arguments )
	{
		StartupProjectDirectory.Initialize();
		ConsoleHelpers.Run( false, () => run( arguments ) );
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
			FilePath inputFilePath = FilePath.FromRelativeOrAbsolutePath( inputFileArgument.Value, DotNetHelpers.GetWorkingDirectoryPath() );
			FilePath outputFilePath = getRelatedFilePath( inputFilePath, outputFileArgument.Value, ".png" );
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
			IReadOnlyList<int> iconSizes = iconSizesArgument.Value.Split( ',' ).Select( int.Parse ).Collect();
			FilePath inputFilePath = FilePath.FromRelativeOrAbsolutePath( inputFileArgument.Value, DotNetHelpers.GetWorkingDirectoryPath() );
			FilePath outputFilePath = getRelatedFilePath( inputFilePath, outputFileArgument.Value, ".ico" );
			convertToWindowsIco( inputFilePath, outputFilePath, iconSizes );
		} );
		if( !argumentParser.TryParse( arguments ) )
			return -1;
		return 0;
	}

	static void convertToPng( FilePath inputFilePath, FilePath outputFilePath, int? maybeWidth, int? maybeHeight )
	{
		if( !inputFilePath.Extension.EqualsIgnoreCase( ".svg" ) )
			throw new GenericException( "input filename does not have an .svg extension" );
		if( !outputFilePath.Extension.EqualsIgnoreCase( ".png" ) )
			throw new GenericException( "output filename does not have an .png extension" );
		Log.Debug( $"Reading {inputFilePath}" );
		using( var svg = new Svg.SKSvg() )
		{
			using( SysIo.FileStream stream = inputFilePath.OpenBinaryForReading() )
				if( svg.Load( stream ) == null )
					throw new GenericException( "Svg.Skia sucks" );
			Sk.SKPicture svgPicture = svg.Picture ?? throw new GenericException( "Svg.Skia sucks" );
			int width = maybeWidth ?? (int)svgPicture.CullRect.Width;
			int height = maybeHeight ?? (int)svgPicture.CullRect.Height;
			generatePngData( svgPicture, width, height, data =>
			{
				using( SysIo.FileStream stream = outputFilePath.CreateBinary() )
					data.SaveTo( stream );
			} );
		}
		return;
	}

	static void convertToWindowsIco( FilePath inputFilePath, FilePath outputFilePath, IReadOnlyList<int> iconSizes )
	{
		if( !inputFilePath.Extension.EqualsIgnoreCase( ".svg" ) )
			throw new GenericException( "input filename does not have an .svg extension" );
		if( !outputFilePath.Extension.EqualsIgnoreCase( ".ico" ) )
			throw new GenericException( "output filename does not have an .ico extension" );
		using( var svg = new Svg.SKSvg() )
		{
			Log.Debug( $"Reading {inputFilePath}" );
			using( SysIo.FileStream stream = inputFilePath.OpenBinaryForReading() )
				if( svg.Load( stream ) == null )
					throw new GenericException( "Svg.Skia sucks" );
			Sk.SKPicture svgPicture = svg.Picture ?? throw new GenericException( "Svg.Skia sucks" );

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

			Log.Debug( $"Writing {outputFilePath}" );
			using( SysIo.FileStream stream = outputFilePath.CreateBinary() )
			{
				// See https://en.wikipedia.org/wiki/ICO_(file_format)
				const int iconDirStructureSize = 6;
				const int iconDirEntryStructureSize = 16;
				using SysIo.BinaryWriter writer = new( stream );
				writer.Write( (short)0 );   // 0 : reserved
				writer.Write( (short)1 );   // 2 : 1=ico, 2=cur
				writer.Write( (short)iconSizes.Count ); // 4 : number of images
				Assert( stream.Length == iconDirStructureSize );
				int imageDataOffset = iconDirStructureSize + iconSizes.Count * iconDirEntryStructureSize;
				for( int i = 0; i < iconSizes.Count; i++ )
				{
					int width = iconSizes[i];
					int height = iconSizes[i];
					int imageDataLength = imageDataArrays[i].Length;
					Assert( stream.Length == iconDirStructureSize + i * iconDirEntryStructureSize );
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

	static FilePath getRelatedFilePath( FilePath sourceFilePath, string? targetFileName, string extension )
	{
		Assert( extension.StartsWith2( "." ) );
		if( targetFileName == null )
			return sourceFilePath.WithReplacedExtension( extension );
		DirectoryPath targetDirectoryPath = DirectoryPath.FromAbsoluteOrRelativePath( targetFileName, DotNetHelpers.GetWorkingDirectoryPath() );
		if( targetFileName.EndsWith( '/' ) || targetFileName.EndsWith( '\\' ) || targetDirectoryPath.Exists() )
			return targetDirectoryPath.File( sourceFilePath.GetFileNameWithoutExtension() + extension );
		return FilePath.FromRelativeOrAbsolutePath( targetFileName, DotNetHelpers.GetWorkingDirectoryPath() );
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
