using System.IO;
using System.Globalization;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace ParallelParsing.GZTool.NET;

// 	/* index_version should be 0 (default), thus omitted */
// 	/* line_number_format should be irrelevant if index contains no info about line number */
// 	/* number_of_lines omitted, for the same reason */

// 	public Index AddPoint(uint bits, ulong input, ulong output, uint left, 
// 		char[] window, uint windowSize, ulong lineNumber, bool isCompressedWindow)
// 	{
// 		// Point next;
// 		// ulong size = windowSize;

// 		// if (isCompressedWindow)
// 		// {
// 		// 	next = new Point {
// 		// 		Output = output,
// 		// 		Input = input,
// 		// 		Bits = bits,

// 		// 	}

// 		// }
// 		// else
// 		// {

// 		// }

// 		// List.Add(next);

// 		return this;
// 	}

// 	public Index()
// 	{
// 		FileSize = 0;
// 		List = new List<Point>(8);
// 		IsIndexComplete = false;
// 		FileName = null;
// 	}

// 	// private unsafe char[]? CompressChunk(char[] source, ulong inputSize, int level)
// 	// {
// 	// 	ZSignal flush;
// 	// 	ZResult ret;
// 	// 	uint have;
// 	// 	ulong i = 0;
// 	// 	ulong outputSize = 0;
// 	// 	FixedArray<char> input, output, outComplete;

// 	// 	if (Defined.DeflateInit(out var strm, level) is not ZResult.OK) return null;

// 	// 	input = new FixedArray<char>(Constants.CHUNK);
// 	// 	output = new FixedArray<char>(Constants.CHUNK);
// 	// 	outComplete = new FixedArray<char>(Constants.CHUNK);

// 	// 	do
// 	// 	{
// 	// 		strm = (uint)(i + Constants.CHUNK < inputSize ? 
// 	// 									Constants.CHUNK : 
// 	// 									inputSize - i);

// 	// 		do
// 	// 		{
// 	// 			strm.NextOut = output;
// 	// 		} while (true);

// 	// 	} while (flush != ZSignal.FINISH);

// 	// 	if (ret != ZResult.STREAM_END) throw new UnreachableException();

// 	// 	Defined.DeflateEnd(strm);
// 	// 	size = outputSize;
// 	// 	return outComplete;
// 	// }
// }

public enum IndexAndExtractionOptions
{
	JustCreateIndex = 1,
	ExtractFromByte,
	CompressAndCreateIndex,
	[Obsolete]
	// it shouldn't be necessary to use any info about line
	ExtractFromLine
}

public enum Action
{
	NotSet,
	ExtractFromByte,
	CreateIndex,
	[Obsolete]
	ExtractFromLine
}

public enum GzipMarkFoundType
{
	Error = 8,
	FatalError,
	None,
	Beginning,
	FullFlush
}

public enum DecompressInAdvanceInitializers
{
	// initial total reset
	Reset,
	// no reset, just continue processing
	Continue,
	// reset all but last_correct_reentry_point_returned
	// in order to continue processing the same gzip stream.
	SoftReset
}

public enum ZResult
{
	OK = 0,
	STREAM_END = 1,
	NEED_DICT = 2,
	ERRNO = -1,
	STREAM_ERROR = -2,
	DATA_ERROR = -3,
	MEM_ERROR = -4,
	BUF_ERROR = -5,
	VERSION_ERROR = -6
}

public enum ZSignal
{
	NO_FLUSH,
	PARTIAL_FLUSH,
	SYNC_FLUSH,
	FULL_FLUSH,
	FINISH,
	BLOCK,
	TREES
}

public enum ExitReturnedValues
{
	OK = 0,
	GENERIC_ERROR = 1,
	INVALID_OPTION = 2,
	FILE_OVERWRITTEN = 100
}

public enum ZFlush
{
	NO_FLUSH,
	PARTIAL_FLUSH,
	SYNC_FLUSH,
	FULL_FLUSH,
	FINISH,
	BLOCK,
	TREES
}

public enum SeekOpt
{
	SET,
	CUR,
	END
}

public struct ZReturn
{
	ZResult Res;
	int Val;

	public static implicit operator ZReturn(int val) => new ZReturn { Val = val };
	public static implicit operator ZReturn(ZResult res) => new ZReturn { Res = res };
	public static implicit operator ZResult(ZReturn ret) => ret.Res;
	public static ZReturn operator --(ZReturn ret) => --ret.Val;
	public int ToInt() => Val;
}

public static class Constants
{
	public const string ZLIB_VERSION = "1.2.11";
	public const int MAX_GIVE_ME_SI_UNIT_RETURNED_LENGTH = 14;
	// [Obsolete]
	// public static char[] number_output = new char[MAX_GIVE_ME_SI_UNIT_RETURNED_LENGTH]; 

	// desired distance between access points
	public const long SPAN = 10485760L;

	// sliding window size
	public const uint WINSIZE = 32768U;

	// file input buffer size
	public const uint CHUNK = 16384;

	// window is an uncompressed WINSIZE size window
	public const uint UNCOMPRESSED_WINDOW = uint.MaxValue;

	// default index version (v0)
	public const string GZIP_INDEX_IDENTIFIER_STRING = "gzipindx";

	// index version with line number info
	public const string GZIP_INDEX_IDENTIFIER_STRING_V1 = "gzipindeX";

	// header size in bytes of gztool's .gzi files
	public const int GZIP_INDEX_HEADER_SIZE = 16;

	// header size in bytes of gzip files created by zlib
	// github.com/madler/zlib/blob/master/zlib.h
	public const int GZIP_INDEX_SIZE_BY_ZLIB = 10;

	// If deflateSetHeader is not used, the default gzip header has text false,
	// the time set to zero, and os set to 255, with no extra, name, or comment fields.
	// default waiting time in seconds when supervising a growing gzip file:
	public const int WAITING_TIME = 4;

	// how many CHUNKs will be decompressed in advance if it is needed (parameter gzip_stream_may_be_damaged, `-p`)
	public const int CHUNKS_TO_DECOMPRESS_IN_ADVANCE = 3;

	// how many CHUNKs will be look to backwards for a new good gzip reentry point after an error is found (with `-p`)
	public const int CHUNKS_TO_LOOK_BACKWARDS = 3;

	public const int EOF = -1;
}

[Obsolete]
public class GenericException : Exception
{
	public GenericException(string message) : base(message) { }
}

[Obsolete]
public class InvalidOptionException : Exception { }

[Obsolete]
public class ExitFileOverwrittenException : Exception { }