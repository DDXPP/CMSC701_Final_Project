using static ParallelParsing.Interop.LibZ;
using ParallelParsing.Common;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Buffers;

namespace ParallelParsing.Interop;

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

public class ZException : Exception
{
	public ZResult Code { get; }

	public ZException(ZResult code)
	{
		Code = code;
	}
}

public unsafe class ZStream : IDisposable
{
	public ZStream() 
	{
		var stream = new z_stream();
		Ptr = (z_stream*)NativeMemory.Alloc((nuint)sizeof(z_stream));
		Marshal.StructureToPtr(stream, (nint)Ptr, false);
	}

	internal z_stream* Ptr { get; init; }

	public ulong TotalIn
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Ptr->total_in;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set => Ptr->total_in = value;
	}

	public ulong TotalOut
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Ptr->total_out;
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set => Ptr->total_out = value;
	}

	public int DataType
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Ptr->data_type;
	}

	private byte[]? _NextIn;
	private GCHandle _HNextIn;
	public uint AvailIn 
	{ 
		[MethodImpl(MethodImplOptions.AggressiveInlining)] 
		get => Ptr->avail_in;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] 
		set => Ptr->avail_in = value;
	}
	public byte[]? NextIn
	{
		get => _NextIn;
		set
		{
			if (_HNextIn != default) _HNextIn.Free();
			_HNextIn = GCHandle.Alloc(value, GCHandleType.Pinned);
			_NextIn = value;
			Ptr->next_in = (byte*)_HNextIn.AddrOfPinnedObject();
		}
	}

	private byte[]? _NextOut;
	private GCHandle _HNextOut;
	public uint AvailOut
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Ptr->avail_out;
		[MethodImpl(MethodImplOptions.AggressiveInlining)] 
		set => Ptr->avail_out = value;
	}
	public byte[]? NextOut
	{
		get => _NextOut;
		set
		{
			if (_HNextOut != default) _HNextOut.Free();
			_HNextOut = GCHandle.Alloc(value, GCHandleType.Pinned);
			_NextOut = value;
			Ptr->next_out = (byte*)_HNextOut.AddrOfPinnedObject();
		}
	}

	public void SetNextInPtr(MemoryHandle hnd) => Ptr->next_in = (byte*)hnd.Pointer;
	public void SetNextOutPtr(MemoryHandle hnd) => Ptr->next_out = (byte*)hnd.Pointer;

	public void Dispose()
	{
		Compat.InflateEnd(this);
		if (_HNextOut != default) _HNextOut.Free();
		if (_HNextIn != default) _HNextIn.Free();
	}
}

public unsafe static class Compat
{
	public static ZResult InflateInit(ZStream strm, int windowBits)
	{
		fixed (char* ver = Constants.ZLIB_VERSION)
		{
			return (ZResult)inflateInit2_(strm.Ptr, windowBits, ver, sizeof(z_stream));
		}
	}
	public static ZResult InflateInit(ZStream strm)
	{
		fixed (char* ver = Constants.ZLIB_VERSION)
		{
			return (ZResult)inflateInit_(strm.Ptr, ver, sizeof(z_stream));
		}
	}

	/// <summary>
	/// Initializes the decompression dictionary from the given uncompressed byte sequence. 
	/// This function must be called immediately after a call of inflate, 
	/// if that call returned Z_NEED_DICT. 
	/// For raw inflate, this function can be called at any time to set the dictionary. 
	/// If the provided dictionary is smaller than the window and 
	/// there is already data in the window, 
	/// then the provided dictionary will amend what's there. 
	/// </summary>
	/// <param name="strm"></param>
	/// <param name="dictionary"></param>
	/// <param name="dictLength"></param>
	/// <returns></returns>
	public static unsafe ZResult InflateSetDictionary(
		ZStream strm, byte[] dictionary, uint dictLength)
	{
		fixed (byte* ptr = dictionary)
		{
			return inflateSetDictionary(strm.Ptr, ptr, dictLength);
		}
	}

	public static ZResult Inflate(ZStream strm, ZFlush flush)
		=> inflate(strm.Ptr, flush);

	public static ZResult InflateEnd(ZStream strm) => inflateEnd(strm.Ptr);

	/// <summary>
	/// This function inserts bits in the inflate input stream. 
	/// The intent is that this function is used to start inflating at a bit position 
	/// in the middle of a byte. 
	/// The provided bits will be used before any bytes are used from next_in. 
	/// This function should only be used with raw inflate, 
	/// and should be used before the first inflate() call after inflateInit2() or inflateReset()
	/// </summary>
	/// <param name="strm"></param>
	/// <param name="bits">must be less than or equal to 16</param>
	/// <param name="value">
	/// many of the least significant bits of value will be inserted in the input
	/// </param>
	/// <returns>Z_OK if success, or Z_STREAM_ERROR 
	/// if the source stream state was inconsistent
	/// </returns>
	public static ZResult InflatePrime(ZStream strm, int bits, int value) 
		=> inflatePrime(strm.Ptr, bits, value);

	public static ZResult InflateReset(ZStream strm, int windowBits)
		=> inflateReset2(strm.Ptr, windowBits);
	
	public static ZResult InflateReset(ZStream strm) => inflateReset(strm.Ptr);
}
