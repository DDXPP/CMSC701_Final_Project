using System.Runtime.InteropServices;
using System.Security;

namespace ParallelParsing.Interop;

[SuppressUnmanagedCodeSecurity]
internal static class LibZ
{
	[DllImport("libz", CharSet = CharSet.Ansi)]
	public static unsafe extern ZResult inflate(z_stream* strm, ZFlush flush);

	[DllImport("libz", CharSet = CharSet.Ansi)]
	public static unsafe extern ZResult inflateEnd(z_stream* strm);

	[DllImport("libz", CharSet = CharSet.Ansi)]
	public static unsafe extern ZResult inflatePrime(z_stream* strm, int bits, int value);

	[DllImport("libz", CharSet = CharSet.Ansi)]
	public static unsafe extern ZResult inflateSetDictionary(
		z_stream* strm, byte* dictionary, uint dictLength);

	[DllImport("libz", CharSet = CharSet.Ansi)]
	public static unsafe extern ZResult inflateInit2_(
		z_stream* strm, int windowBits, char* version, int stream_size);

	[DllImport("libz", CharSet = CharSet.Ansi)]
	public static unsafe extern ZResult inflateInit_(
		z_stream* strm, char* version, int stream_size);

	[DllImport("libz")]
	public static unsafe extern ZResult inflateReset2(z_stream* strm, int windowBits);
	
	[DllImport("libz")]
	public static unsafe extern ZResult inflateReset(z_stream* strm);
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct z_stream
{
	/// <summary>
	/// next input byte
	/// </summary>
	// [MarshalAs(UnmanagedType.LPArray)]
	public byte* next_in;
	/// <summary>
	/// number of bytes available at next_in
	/// </summary>
	public uint avail_in;
	/// <summary>
	/// total number of input bytes read so far
	/// </summary>
	public ulong total_in;
	/// <summary>
	/// next output byte will go here
	/// </summary>
	// [MarshalAs(UnmanagedType.LPArray)]
	public byte* next_out;
	/// <summary>
	/// remaining free space at next_out
	/// </summary>
	public uint avail_out;
	/// <summary>
	/// total number of bytes output so far
	/// </summary>
	public ulong total_out;

	// [MarshalAs(UnmanagedType.LPStr)]
	public IntPtr msg;
	public void* state;
	public void* zalloc;
	public void* zfree;
	public void* opaque;
	public int data_type;
	public ulong adler;
	public ulong reserved;
}
