
using static ParallelParsing.Common.Constants;
using static ParallelParsing.Interop.Compat;
using ParallelParsing.Common;
using System.Buffers;
using ParallelParsing.Interop;
using Index = ParallelParsing.Common.Index;
using Point = ParallelParsing.Common.Point;

namespace ParallelParsing;

public static class Core
{
	public static Index BuildDeflateIndex(FileStream file, uint chunksize)
	{
		using var strm = new ZStream();
		Index index = new Index();
		byte[] input = new byte[CHUNK];
		byte[] window = new byte[WINSIZE];

		int recordCounter = 0;
		int prevAvailOut = 0;
		byte[] offsetBeforePoint = new byte[WINSIZE];
		int offsetArraySize = 0;

			ZResult ret;
			long totin, totout;

			// automatic gzip decoding
			ret = InflateInit(strm, 47);
			if (ret != ZResult.OK)
			{
				throw new ZException(ret);
			}

			totin = totout = 0;
			strm.AvailOut = 0;
			do
			{
				// get some compressed data from input file
				strm.AvailIn = (uint)file.Read(input, 0, (int)CHUNK);
				if (strm.AvailIn == 0)
				{
					throw new ZException(ZResult.DATA_ERROR);
				}
				strm.NextIn = input;

				// process all of that, or until end of stream
				do
				{
					// reset sliding window if necessary
					if (strm.AvailOut == 0)
					{
						strm.AvailOut = WINSIZE;
						strm.NextOut = window;
					}

					// inflate until out of input, output, or at end of block --
					// update the total input and output counters
					totin += strm.AvailIn;
					totout += strm.AvailOut;

					// return at end of block
					ret = Inflate(strm, ZFlush.BLOCK);

					totin -= strm.AvailIn;
					totout -= strm.AvailOut;
					if (ret == ZResult.NEED_DICT ||
						ret == ZResult.MEM_ERROR ||
						ret == ZResult.DATA_ERROR ||
						ret == ZResult.STREAM_ERROR ||
						ret == ZResult.BUF_ERROR ||
						ret == ZResult.VERSION_ERROR)
						throw new ZException(ret);

					if (strm.NextOut != null)
					{
						// Count how many "@"s are in NextIn
						int currNextOutLength = strm.NextOut.Length;
						int iStartPos = prevAvailOut == 0 ? 0 : currNextOutLength - prevAvailOut;

						for (int i = iStartPos; i < currNextOutLength - strm.AvailOut; i++)
						{
							var c = strm.NextOut[i];

							if (c == 64)
							{
								recordCounter++;
								Array.Clear(offsetBeforePoint, 0, offsetBeforePoint.Length);
								offsetArraySize = 0;
							}

							offsetBeforePoint[offsetArraySize] = c;
							offsetArraySize++;
						}
						prevAvailOut = strm.AvailOut > 0 ? (int)strm.AvailOut : 0;

						if ((strm.DataType & 128) != 0 && (strm.DataType & 64) == 0)
						{
							// Add the first point after the header
							if (totout == 0)
								index.AddPoint(strm.DataType & 7, totin, totout, strm.AvailOut, window, new byte[0]);
							else
							{
								if (recordCounter > chunksize - 8)
								{
									index.AddPoint(strm.DataType & 7, totin, totout, strm.AvailOut, window, offsetBeforePoint[0..offsetArraySize]);
									recordCounter = 0;
								}
							}
						}
					}

					if (ret == ZResult.STREAM_END)
					{
						if (strm.AvailIn != 0 || file.Position != file.Length)
						{
							ret = InflateReset(strm);
							if (ret != ZResult.OK)
								throw new ZException(ret);
							continue;
						}
						index.AddPoint(strm.DataType & 7, totin, totout, strm.AvailOut, window, new byte[0]);
						break;
					}

				} while (strm.AvailIn != 0);
			} while (ret != ZResult.STREAM_END);

			return index;
	}

	public static unsafe int ExtractDeflateIndex(
		Memory<byte> fileBuffer, Point from, Point to, Memory<byte> buf)
	{
		using var strm = new ZStream();
		Memory<byte> input;
		MemoryHandle hInput = new MemoryHandle();
		MemoryHandle hBuf = new MemoryHandle();
		var len = (int)(to.Output - from.Output);

		ZResult ret;
		int value = 0;

		if (len < 0)
			return 0;

		ret = InflateInit(strm, -15);
		if (ret != ZResult.OK) throw new ZException(ret);

		var posInFile = from.Bits == 0 ? 1 : 0;
		if (from.Bits != 0)
		{
			value = fileBuffer.Span[0];
			InflatePrime(strm, from.Bits, value >> (8 - from.Bits));
			posInFile++;
		}
		InflateSetDictionary(strm, from.Window, WINSIZE);

		strm.AvailIn = 0;
		strm.AvailOut = (uint)len;
		hBuf = buf.Pin();
		strm.SetNextOutPtr(hBuf);
		do
		{
			if (strm.AvailIn == 0)
			{
				value = Math.Min(CHUNK, fileBuffer.Length - posInFile);
				input = fileBuffer.Slice(posInFile, value);
				hInput.Dispose();
				hInput = input.Pin();
				strm.AvailIn = (uint)value;
				posInFile += value;
				if (value == 0) throw new ZException(ZResult.DATA_ERROR);				
				strm.SetNextInPtr(hInput);
			}
			ret = Inflate(strm, ZFlush.NO_FLUSH);
			if (ret == ZResult.MEM_ERROR || ret == ZResult.DATA_ERROR || ret == ZResult.NEED_DICT)
				throw new ZException(ret);
			if (ret == ZResult.STREAM_ERROR)
			{
				Console.WriteLine("stream error");
				break;
			}
			if (ret == ZResult.STREAM_END) break;

		} while (strm.AvailOut != 0);

		hInput.Dispose();
		hBuf.Dispose();
		return len - (int)strm.AvailOut;
	}
}
