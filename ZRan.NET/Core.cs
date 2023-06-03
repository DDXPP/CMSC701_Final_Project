
using static ParallelParsing.ZRan.NET.Constants;
// using static ParallelParsing.ZRan.NET.Compat;
using static Zlib.Extended.Inflate;
using System.IO.Compression;
using System.Text;
using System.Runtime.InteropServices;
using static Zlib.Extended.Zlib;
using Zlib.Extended.Enumerations;

namespace ParallelParsing.ZRan.NET;

public static class Core
{
	// Latest version, built on the old BuildDeflateIndex() 
	public static Index BuildDeflateIndex_NEW(FileStream file, long span, uint chunksize)
	{
		z_stream strm = new();
		Index index = new Index(0);
		byte[] input = new byte[CHUNK];
		byte[] window = new byte[WINSIZE];

		int recordCounter = 0;
		int prevAvailOut = 0;
		byte[] offsetBeforePoint = new byte[WINSIZE];
		int offsetArraySize = 0;

		try
		{
			ZResult ret;
			// our own total counters to avoid 4GB limit
			long totin, totout;
			// totout value of last access point
			// long last;

			// automatic gzip decoding
			ret = (ZResult)inflateInit2(strm, 47);
			if (ret != ZResult.OK)
			{
				throw new ZException(ret);
			}

			// inflate the input, maintain a sliding window, and build an index -- this
			// also validates the integrity of the compressed data using the check
			// information in the gzip or zlib stream
			totin = totout = 0;
			strm.avail_out = 0;
			do
			{
				// get some compressed data from input file
				strm.avail_in = (uint)file.Read(input, 0, (int)CHUNK);
				// if (ferror(@in) != 0)
				// {
				// 	throw new ZException(ZResult.ERRNO);
				// }
				if (strm.avail_in == 0)
				{
					throw new ZException(ZResult.DATA_ERROR);
				}
				strm.in_buf = input;

				// process all of that, or until end of stream
				do
				{
					// reset sliding window if necessary
					if (strm.avail_out == 0)
					{
						strm.avail_out = WINSIZE;
						strm.out_buf = window;
					}

					// inflate until out of input, output, or at end of block --
					// update the total input and output counters
					totin += strm.avail_in;
					totout += strm.avail_out;

					// return at end of block
					ret = (ZResult)inflate(strm, FlushValue.Z_BLOCK);

					totin -= strm.avail_in;
					totout -= strm.avail_out;
					if (ret == ZResult.NEED_DICT ||
						ret == ZResult.MEM_ERROR ||
						ret == ZResult.DATA_ERROR ||
						ret == ZResult.STREAM_ERROR ||
						ret == ZResult.BUF_ERROR ||
						ret == ZResult.VERSION_ERROR)
						throw new ZException(ret);
					

					//------------------------------------------------------
					// Count how many "@"s are in NextIn
					int currNextOutLength = strm.out_buf.Length;
					int iStartPos = prevAvailOut == 0 ? 0 : currNextOutLength - prevAvailOut;

					for (int i = iStartPos; i < currNextOutLength - strm.avail_out; i++)
					{
						var c = strm.out_buf[i];

						if (c == 64)
						{
							recordCounter++;
							Array.Clear(offsetBeforePoint, 0, offsetBeforePoint.Length);
							offsetArraySize = 0;
						}

						offsetBeforePoint[offsetArraySize] = c;
						offsetArraySize++;
					}
					prevAvailOut = strm.avail_out > 0 ? (int)strm.avail_out : 0;

					// if at end of block, consider adding an index entry (note that if
					// data_type indicates an end-of-block, then all of the
					// uncompressed data from that block has been delivered, and none
					// of the compressed data after that block has been consumed,
					// except for up to seven bits) -- the totout == 0 provides an
					// entry point after the zlib or gzip header, and assures that the
					// index always has at least one access point; we avoid creating an
					// access point after the last block by checking bit 6 of data_type
					if ((((inflate_state)strm.state).mode == inflate_mode.TYPE) && ((inflate_state)strm.state).last == 0)
					{
						// Add the first point after the header
						if (totout == 0) 
						{
							index.AddPoint_NEW((int)((inflate_state)strm.state).bits, totin, totout, strm.avail_out, window, null);
							// last = totout;
						}
						else
						{
							if (recordCounter > chunksize * 0.9)
							{
								// Array.Resize(ref offsetBeforePoint, offsetArraySize + 1);
								// offsetBeforePoint.PrintASCII(offsetBeforePoint.Length);

								index.AddPoint_NEW((int)((inflate_state)strm.state).bits, totin, totout, strm.avail_out, window, offsetBeforePoint[0..offsetArraySize]);
								// Console.WriteLine(index.List.Count());
								
								// Array.Resize(ref offsetBeforePoint, (int)WINSIZE);
								recordCounter = 0;
							}
						}
					}

					if (ret == ZResult.STREAM_END)
					{
						if (strm.avail_in != 0 || file.Position != file.Length)
						{
							ret = (ZResult)inflateReset(strm);
							if (ret != ZResult.OK)
								throw new ZException(ret);
							continue;
						}

						index.AddPoint_NEW((int)((inflate_state)strm.state).bits, totin, totout, strm.avail_out, window, null);

						break;
					}

				} while (strm.avail_in != 0);
			} while (ret != ZResult.STREAM_END);

			// index.length = totout;
			return index;
		}
		finally
		{
			// clean up and return index (release unused entries in list)
			inflateEnd(strm);
		}
	}

	private static int TryCopy<T>(T[] src, int srcOffset, T[] dst,
		int length) where T : unmanaged
	{
		var minDstLen = Math.Min(dst.Length, length);
		var srcDstLen = Math.Min(src.Length - srcOffset, minDstLen);
		Array.Copy(src, srcOffset, dst, 0, srcDstLen);
		return srcDstLen;
	}

	public static unsafe int ExtractDeflateIndex(
		byte[] fileBuffer, Point from, Point to, byte[] buf)
	{
		// GC.Collect();
		// lock (o) {
		// no need to pin (I guess); it's an unmanaged struct on stack
		var strm = new z_stream();
		byte[] input = new byte[CHUNK];
		var len = (int)(to.Output - from.Output);

		ZResult ret;
		int value = 0;

		// proceed only if something reasonable to do
		if (len < 0)
			return 0;

		// raw inflate
		ret = (ZResult)inflateInit2(strm, -15);
		if (ret != ZResult.OK) throw new ZException(ret);

		var posInFile = from.Bits == 0 ? 1 : 0;
		if (from.Bits != 0)
		{
			value = fileBuffer[0];
			inflatePrime(strm, from.Bits, value >> (8 - from.Bits));
			posInFile++;
		}
		inflateSetDictionary(strm, from.Window, WINSIZE);

		strm.avail_in = 0;
		strm.avail_out = (uint)len;

		strm.out_buf = buf;
		do
		{
			if (strm.avail_in == 0)
			{
				value = TryCopy(fileBuffer, posInFile, input, (int)CHUNK);
				strm.avail_in = (uint)value;
				posInFile += value;
				if (value == 0) throw new ZException(ZResult.DATA_ERROR);
				strm.in_buf = input;
			}
			ret = (ZResult)inflate(strm, (int)ZFlush.NO_FLUSH);
			// strm.NextOut.PrintASCII(32*1024-1);
			// normal inflate
			if (ret == ZResult.MEM_ERROR || ret == ZResult.DATA_ERROR || ret == ZResult.NEED_DICT)
				throw new ZException(ret);
			if (ret == ZResult.STREAM_ERROR)
			{
				Console.WriteLine("stream error");
				break;
			}
			if (ret == ZResult.STREAM_END) break;

			// continue to process the available input before reading more
		} while (strm.avail_out != 0);

		// compute the number of uncompressed bytes read after the offset
		return len - (int)strm.avail_out;
	}
	// }
}
