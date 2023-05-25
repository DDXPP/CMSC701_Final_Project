
using static ParallelParsing.ZRan.NET.Constants;
using static ParallelParsing.ZRan.NET.Compat;
using System.IO.Compression;
using System.Text;
using System.Runtime.InteropServices;

namespace ParallelParsing.ZRan.NET;

public static class Core
{
	/// <summary>
	/// Make one entire pass through a zlib or gzip compressed stream and build an
	/// index, with access points about every span bytes of uncompressed output.
	/// gzip files with multiple members are indexed in their entirety. 
	/// </summary>
	/// <param name="span">
	/// span should be chosen to balance the speed of random access against the memory 
	/// requirements of the list, about 32K bytes per access point. 
	/// </param>
	public static Index BuildDeflateIndex(FileStream file, uint chunksize)
	{
		//DELETE THIS!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
		int temp = 0;
		int temp2 = 0;






		ZStream strm = new();
		Index index = new Index(chunksize);
		byte[] input = new byte[CHUNK];
		byte[] window = new byte[WINSIZE];
		
		// Find in which "NextIn"s the checkpoints are going to appear
		List<int> pointAppearsInInputBuffer = new List<int>();
		GetInputBufferIndexForSlowerRead(file, chunksize, pointAppearsInInputBuffer, index);
		// foreach (int idx in pointAppearsInInputBuffer) Console.WriteLine(idx);

		// Reset file stream position for a new round of reading
		file.Position = 0;

		// window from the previous iteration
		byte[] prevWindow = new byte[WINSIZE];

		// Count records so we know where/when to add a point
		int recordCounter = 0;

		// totout from the previous iteration
		long prevTotout = 0;

		// Keep track of which NextIn is being read. Switch to single byte
		// reading mode if the NextIn contains point position. 
		int inputBufferCounter = 0;

		// When in single byte reading mode, treat those 16k reads as one
		// so that inputBufferCount will only increment once.
		int SingleByteReadingModeByteCounter = 0;

		/* Edge case: when decompressing the last iteration of NextIn, it is 
		   possible that the output (i.e., uncompressed data) from that 
		   iteration is not able to fill the entire 32K in NextOut. In this 
		   case, the first NextOut from the next NextIn will be the same as 
		   the current NextOut. Thus, we need to pay attention not to recount 
		   things like recordCounter and totout. */
		// prevAvailOut is used to indicate this edge case.
		int prevAvailOut = 0;

		try
		{
			ZResult ret;
			// our own total counters to avoid 4GB limit
			long totin = 0;
			long totout = 0;
			// totout value of last access point
			long last = 0;

			// 47: enable zlib and gzip decoding with automatic header detection
			ret = InflateInit(strm, 47);
			if (ret != ZResult.OK)
			{
				throw new ZException(ret);
			}
			strm.AvailOut = 0;

			// inflate the input, maintain a sliding window, and build an index -- this
			// also validates the integrity of the compressed data using the check
			// information in the gzip or zlib stream
			do
			{
				inputBufferCounter++;
				bool hasPoint = pointAppearsInInputBuffer.Contains(inputBufferCounter);

				strm.AvailIn = (uint)file.Read(input, 0, hasPoint ? 1 : (int)CHUNK);


				// get some compressed data from input file
				// strm.AvailIn = (uint)file.Read(input, 0, (int)CHUNK);

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
					// * Z_BLOCK requests that inflate() stop if and when it gets to 
					// * the next deflate block boundary. When decoding the zlib or gzip format, 
					// * this will cause inflate() to return immediately after the header and 
					// * before the first block. 
					// * The Z_BLOCK option assists in appending to or combining deflate streams. 
					// * To assist in this, on return inflate() always sets strm->data_type 
					// * to the number of unused bits in the last byte taken from strm->next_in, 
					// * plus 64 if inflate() is currently decoding the last block 
					// * in the deflate stream, plus 128 if inflate() returned immediately 
					// * after decoding an end-of-block code or decoding the complete header 
					// * up to just before the first byte of the deflate stream. 
					// * The end-of-block will not be indicated until all of the uncompressed data 
					// * from that block has been written to strm->next_out. 
					// * The number of unused bits may in general be greater than seven, 
					// * except when bit 7 of data_type is set, 
					// * in which case the number of unused bits will be less than eight. 
					// * data_type is set as noted here every time inflate() returns 
					// * for all flush options, and so can be used to determine the amount of 
					// * currently consumed input in bits. 
					// return at end of block
					ret = Inflate(strm, ZFlush.BLOCK);
								if (chunksize == 1400 && temp == 47) Console.WriteLine(++temp2);

					totin -= strm.AvailIn;
					totout -= strm.AvailOut;

					if (ret == ZResult.NEED_DICT ||
						ret == ZResult.MEM_ERROR ||
						ret == ZResult.DATA_ERROR)
						throw new ZException(ret);
					if (ret == ZResult.STREAM_END)
					{
						if (strm.AvailIn != 0 || file.Position != file.Length)
						{
							ret = InflateReset(strm);
							if (ret != ZResult.OK)
								throw new ZException(ret);
							continue;
						}
						break;
					}


					//-----------------------------------------------------------------
					var len = strm.NextOut.Length;
					int bytesBeforeTargetAt = 0;

					for (int i = ((hasPoint && prevAvailOut != 0) || (inputBufferCounter != 0 && prevAvailOut != 0)) ? len - prevAvailOut : 0;
						i < len - strm.AvailOut; i++)
					{
						var c = strm.NextOut[i];

						if (c != 64)
						{
							bytesBeforeTargetAt++;
						}

						// '@' = 64
						if (c == 64)
						{
							recordCounter++;
							if (recordCounter == index.ChunkSize + 1)
							{
								long tempTotin = 0;
								long tempTotout = 0;
								byte[] tempWindow = new byte[WINSIZE];
								int tempLength = len - (int)strm.AvailOut - (((hasPoint && prevAvailOut != 0) || (inputBufferCounter != 0 && prevAvailOut != 0)) ? len - prevAvailOut : 0);
								int bytesToCopyFromPrev = 0;
								int usefulBytesInCurrentWindow = 0;

								// ASSUME THE END OF A RECORD IS NEW LINE 
								if (bytesBeforeTargetAt == 0)
								{
									tempTotin = totin - 1;
									tempTotout = prevTotout;
								}
								else if (bytesBeforeTargetAt > 0)
								{
									tempTotin = totin;
									tempTotout = totout - (tempLength - bytesBeforeTargetAt);
								}

								usefulBytesInCurrentWindow = (int)tempTotout % (int)WINSIZE;
								bytesToCopyFromPrev = (int)WINSIZE - (int)usefulBytesInCurrentWindow;
								Array.Copy(prevWindow, usefulBytesInCurrentWindow, tempWindow, 0, bytesToCopyFromPrev);
								Array.Copy(window, 0, tempWindow, bytesToCopyFromPrev, usefulBytesInCurrentWindow);

								index.AddPoint(strm.DataType & 7, tempTotin, tempTotout, strm.AvailOut, tempWindow);
								recordCounter = 1;
								// strm.NextOut.PrintASCII(1000);
								// Console.WriteLine("Add point----------------------------------------");
								// Console.WriteLine("prevTotout: " + prevTotout);
								// Console.WriteLine("length: " + tempLength);
								// Console.WriteLine("totin:  " + tempTotin);
								// Console.WriteLine("totout: " + tempTotout);
								// strm.NextOut.PrintASCIIFromTo((((hasPoint && prevAvailOut != 0) || (inputBufferCounter != 0 && prevAvailOut != 0)) ? len-prevAvailOut : 0), tempLength);
								Console.WriteLine(++temp);
							}
						}

					}

					prevTotout = totout;
					Array.Copy(window, prevWindow, WINSIZE);

					if (strm.AvailOut > 0)
					{
						prevAvailOut = (int)strm.AvailOut;
					}
					else
					{
						prevAvailOut = 0;
					}


					// if at end of block, consider adding an index entry (note that if
					// data_type indicates an end-of-block, then all of the
					// uncompressed data from that block has been delivered, and none
					// of the compressed data after that block has been consumed,
					// except for up to seven bits) -- the totout == 0 provides an
					// entry point after the zlib or gzip header, and assures that the
					// index always has at least one access point; we avoid creating an
					// access point after the last block by checking bit 6 of data_type
					// * strm.DataType: the number of unused bits 
					// * in the last byte taken from strm->next_in
					// * +64 if inflate() is currently decoding the last block in the deflate stream
					// * +128 if inflate() returned immediately after decoding an end-of-block code
					// *      or decoding the complete header up to just before the first byte 
					// *      of the deflate stream
				} while (strm.AvailIn != 0);

				if (hasPoint)
				{
					SingleByteReadingModeByteCounter++;
					if (SingleByteReadingModeByteCounter < (int)CHUNK)
					{
						inputBufferCounter--;
					}
					else
					{
						SingleByteReadingModeByteCounter = 0;
					}
				}
				else
				{
					SingleByteReadingModeByteCounter = 0;
				}

			} while (ret != ZResult.STREAM_END);

			// index.length = totout;
			return index;
		}
		finally
		{
			// clean up and return index (release unused entries in list)
			InflateEnd(strm);
		}
	}

	public static void GetInputBufferIndexForSlowerRead(FileStream file, uint chunksize, List<int> pointAppearsInInputBuffer, Index index)
	{
		ZStream strm = new();
		byte[] input = new byte[CHUNK];
		byte[] window = new byte[WINSIZE];
		int recordCounter = 0;
		int inputBufferCounter = 0;
		int prevRecordCounter = 0;
		int prevAvailOut = 0;

		try
		{
			ZResult ret;
			// our own total counters to avoid 4GB limit
			long totin = 0;
			long totout = 0;
			// totout value of last access point
			long last = 0;

			// 47: enable zlib and gzip decoding with automatic header detection
			ret = InflateInit(strm, 47);
			if (ret != ZResult.OK)
			{
				throw new ZException(ret);
			}
			strm.AvailOut = 0;

			// inflate the input, maintain a sliding window, and build an index -- this
			// also validates the integrity of the compressed data using the check
			// information in the gzip or zlib stream
			do
			{
				inputBufferCounter++;

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
					if (strm.DataType == 128 && index.List.Count == 0)
						index.AddPoint(strm.DataType & 7, totin - strm.AvailIn, strm.AvailOut, 0, window);

					totin -= strm.AvailIn;
					totout -= strm.AvailOut;

					if (ret == ZResult.NEED_DICT ||
						ret == ZResult.MEM_ERROR ||
						ret == ZResult.DATA_ERROR)
						throw new ZException(ret);
					if (ret == ZResult.STREAM_END)
					{
						if (strm.AvailIn != 0 || file.Position != file.Length)
						{
							ret = InflateReset(strm);
							if (ret != ZResult.OK)
								throw new ZException(ret);
							continue;
						}
						break;
					}

					var len = strm.NextOut.Length;

					for (int i = (inputBufferCounter != 1 && prevAvailOut != 0) ? len - prevAvailOut : 0; i < len - strm.AvailOut; i++)
					{
						var c = strm.NextOut[i];
						// '@' = 64
						if (c == 64)
						{
							recordCounter++;
							if (recordCounter == index.ChunkSize + 1)
							{
								pointAppearsInInputBuffer.Add(inputBufferCounter);
								recordCounter = 1;
							}
						}
					}

					if (strm.AvailOut > 0)
					{
						prevAvailOut = (int)strm.AvailOut;
					}
					else
					{
						prevAvailOut = 0;
					}

					prevRecordCounter = recordCounter;
				} while (strm.AvailIn != 0);
			} while (ret != ZResult.STREAM_END);
		}
		finally
		{
			// clean up 
			InflateEnd(strm);
		}
	}


	/// <summary>Use the index to read len bytes from offset into buf. </summary>
	/// <param name="offset">starting from `offset` of uncompressed bytes</param>
	/// <param name="len">count of uncompressed bytes</param>
	/// <returns>
	/// bytes read. If data is requested past the end of the uncompressed data, 
	/// then deflate_index_extract() will return a value less than len, 
	/// indicating how much was actually read into buf. 
	/// This function should not throw a data error unless the file was modified since
	/// the index was generated, since deflate_index_build() validated all of the
	/// input. deflate_index_extract() will return Z_ERRNO if there is an error on
	/// reading or seeking the input file.
	/// </returns>
	public static Index BuildDeflateIndex(FileStream file, long span)
	{
		ZStream strm = new();
		Index index = new Index(0);
		byte[] input = new byte[CHUNK];
		byte[] window = new byte[WINSIZE];

		try
		{
			ZResult ret;
			// our own total counters to avoid 4GB limit
			long totin, totout;
			// totout value of last access point
			long last;

			// automatic gzip decoding
			ret = InflateInit(strm, 47);
			if (ret != ZResult.OK)
			{
				throw new ZException(ret);
			}

			// inflate the input, maintain a sliding window, and build an index -- this
			// also validates the integrity of the compressed data using the check
			// information in the gzip or zlib stream
			totin = totout = last = 0;
			strm.AvailOut = 0;
			do
			{
				// get some compressed data from input file
				strm.AvailIn = (uint)file.Read(input, 0, (int)CHUNK);
				// if (ferror(@in) != 0)
				// {
				// 	throw new ZException(ZResult.ERRNO);
				// }
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
						ret == ZResult.DATA_ERROR)
						throw new ZException(ret);
					if (ret == ZResult.STREAM_END)
					{
						if (strm.AvailIn != 0 || file.Position != file.Length)
						{
							ret = InflateReset(strm);
							if (ret != ZResult.OK)
								throw new ZException(ret);
							continue;
						}
						break;
					}

					// if at end of block, consider adding an index entry (note that if
					// data_type indicates an end-of-block, then all of the
					// uncompressed data from that block has been delivered, and none
					// of the compressed data after that block has been consumed,
					// except for up to seven bits) -- the totout == 0 provides an
					// entry point after the zlib or gzip header, and assures that the
					// index always has at least one access point; we avoid creating an
					// access point after the last block by checking bit 6 of data_type
					if ((strm.DataType & 128) != 0 && (strm.DataType & 64) == 0 &&
						(totout == 0 || totout - last > span))
					{
						index.AddPoint(strm.DataType & 7, totin, totout, strm.AvailOut, window);
						last = totout;
					}
				} while (strm.AvailIn != 0);
			} while (ret != ZResult.STREAM_END);

			// index.length = totout;
			return index;
		}
		finally
		{
			// clean up and return index (release unused entries in list)
			InflateEnd(strm);
		}
	}

	public static int ExtractDeflateRange2(byte[] fileBuffer, Point from, Point to, byte[] buf)
	{
		var strm = new ZStream();
		var input = new byte[CHUNK];
		var discard = new byte[WINSIZE];
		byte[] window = new byte[WINSIZE];

		var fileBufferOffset = 0;
		ZResult res;

		var tempBuf = new List<byte>();
		int prevTotout = 0;
		int inputRange = (int)to.Input - (int)from.Input;


		try
		{
			res = InflateInit(strm, -15);
			if (res != ZResult.OK) throw new ZException(res);

			InflateSetDictionary(strm, from.Window, WINSIZE);

			strm.AvailIn = 0;
			// strm.AvailOut = (uint)(to.Output - from.Output);
			strm.AvailOut = WINSIZE;
			strm.NextOut = buf;

			do
			{

				if (strm.AvailIn == 0)
				{
					var count = (uint)TryCopy(fileBuffer, fileBufferOffset, input, (int)CHUNK);
					if (count == 0) throw new ZException(ZResult.DATA_ERROR);

					strm.AvailIn = count;
					strm.NextIn = input;

					fileBufferOffset += (int)CHUNK;
				}

				do
				{
					if (strm.AvailOut == 0)
					{
						strm.AvailOut = WINSIZE;
						strm.NextOut = window;
					}

					res = Inflate(strm, ZFlush.BLOCK); 
					// strm.NextOut.PrintASCIIFirstAndLast(1000);

					if (!(strm.AvailIn == 0 && (int)strm.TotalIn < inputRange))
					{
						tempBuf.AddRange(strm.NextOut.Take((int)strm.TotalOut - prevTotout)); 
						prevTotout = (int)strm.TotalOut;
					}

					// Console.WriteLine("--------------------------------");
					
					//---------------------

					if (res == ZResult.MEM_ERROR ||
						res == ZResult.DATA_ERROR ||
						res == ZResult.NEED_DICT)
						throw new ZException(res);

					if (res == ZResult.STREAM_END)
					{
						if (strm.AvailIn < 8)
						{
							fileBufferOffset += (int)(8 - strm.AvailIn);
							strm.AvailIn = 0;
						}

						break;
					}
				} while (strm.AvailIn != 0);

			} while ((int)strm.TotalIn < inputRange);
			// } while (strm.AvailIn != 0);

			return (int)(to.Output - from.Output - strm.AvailOut);
		}
		finally
		{
			// convert list to array
			buf = tempBuf.ToArray();
			// buf.PrintASCII(buf.Count());
			buf.PrintASCIIFirstAndLast(2000);

			InflateEnd(strm);
		}
	}

	// the chunk size parameter can be at most as large as 1 million
	// otherwise it'll surpass the 2GB object limit
	// input buffer begins with the remaining bits with a size indicated by start.Bits
	public static unsafe int ExtractDeflateRange(
		byte[] fileBuffer, Point start, byte[] buf, int len)
	{
		ZStream strm = new();
		byte[] input = new byte[CHUNK];
		byte[] discard = new byte[WINSIZE];
		var fileBufferOffset = 0;

		try
		{
			ZResult ret;
			int value = 0;

			// raw inflate
			// - -windowBits determines the window size
			// - not looking for a zlib or gzip header
			// - not generating a check value
			// - not looking for any check values for comparison at the end of the stream
			ret = InflateInit(strm, -15);
			if (ret != ZResult.OK)
				throw new ZException(ret);

			// file.Seek(start.Input - (start.Bits != 0 ? 1 : 0), SeekOrigin.Begin);
			// if (start.Bits != 0)
			// {
			// 	// ret = (ZResult)file.ReadByte();
			// 	// if (ret == ZResult.ERRNO)
			// 	// {
			// 	// 	throw new ZException(ZResult.DATA_ERROR);
			// 	// }
			// 	InflatePrime(strm, start.Bits, fileBuffer[0] >> (8 - start.Bits));
			// 	fileBufferOffset++;
			// }
			InflateSetDictionary(strm, start.Window, WINSIZE);

			strm.AvailIn = 0;

			// at offset now
			strm.AvailOut = (uint)len;
			strm.NextOut = buf;

			// uncompress until avail_out filled, or end of stream
			do
			{
				if (strm.AvailIn == 0)
				{
					strm.AvailIn = (uint)TryCopy(fileBuffer, fileBufferOffset, input, (int)CHUNK);
					// strm.AvailIn = (uint)file.Read(input, 0, (int)CHUNK);
					// if (strm.AvailIn == 0)
					// {
					// 	throw new ZException(ZResult.DATA_ERROR);
					// }
					strm.NextIn = input;
				}
				ret = Inflate(strm, ZFlush.NO_FLUSH);
				// normal inflate
				if (ret == ZResult.MEM_ERROR || ret == ZResult.DATA_ERROR || ret == ZResult.NEED_DICT)
					throw new ZException(ret);
				if (ret == ZResult.STREAM_END)
				{
					// near the end of a gzip member, which might be followed by
					// another gzip member -- skip the gzip trailer and see if
					// there is more input after it
					if (strm.AvailIn < 8)
					{
						fileBufferOffset += (int)(8 - strm.AvailIn);
						// file.Seek(8 - strm.AvailIn, SeekOrigin.Current);
						strm.AvailIn = 0;
					}
					else

					if (strm.AvailIn == 0 && fileBufferOffset == fileBuffer.Length)
						// the input ended after the gzip trailer -- done
						break;

					// there is more input, so another gzip member should follow --
					// validate and skip the gzip header
					ret = InflateReset(strm, 31);
					if (ret != ZResult.OK)
						throw new ZException(ret);
					do
					{
						if (strm.AvailIn == 0)
						{
							strm.AvailIn = (uint)TryCopy(
								fileBuffer, fileBufferOffset, input, (int)CHUNK);

							if (strm.AvailIn == 0)
							{
								ret = ZResult.DATA_ERROR;
								throw new ZException(ZResult.DATA_ERROR);
							}
							strm.NextIn = input;
						}
						ret = Inflate(strm, ZFlush.BLOCK);
						if (ret == ZResult.MEM_ERROR || ret == ZResult.DATA_ERROR)
							throw new ZException(ret);
					} while ((strm.DataType & 128) == 0);

					// set up to continue decompression of the raw deflate stream
					// that follows the gzip header
					ret = InflateReset(strm, -15);
					if (ret != ZResult.OK)
						throw new ZException(ret);
				}

				// continue to process the available input before reading more
			} while (strm.AvailOut != 0);

			// compute the number of uncompressed bytes read after the offset
			value = len - (int)strm.AvailOut;

			return value;
		}
		finally
		{
			// clean up and return the bytes read, or the negative error
			InflateEnd(strm);
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

	public static int ExtractDeflateIndex(
		FileStream file, Index index, long offset, byte[] buf, int len)
	{
		// no need to pin (I guess); it's an unmanaged struct on stack
		ZStream strm = new();
		byte[] input = new byte[CHUNK];
		byte[] discard = new byte[WINSIZE];

		try
		{
			ZResult ret;
			int value = 0;
			bool skip;
			Point here;
			var streamOffset = 0;

			// proceed only if something reasonable to do
			if (len < 0)
				return 0;

			// find where in stream to start
			value = index.List.Count;
			while (--value != 0 && index.List[streamOffset + 1].Output <= offset)
				streamOffset++;
			here = index.List[streamOffset];

			// raw inflate
			ret = InflateInit(strm, -15);
			if (ret != ZResult.OK)
				throw new ZException(ret);
			file.Seek(here.Input - (here.Bits != 0 ? 1 : 0), SeekOrigin.Begin);
			if (here.Bits != 0)
			{
				value = file.ReadByte();
				if (value == -1)
				{
					throw new ZException(ZResult.DATA_ERROR);
				}
				InflatePrime(strm, here.Bits, value >> (8 - here.Bits));
			}
			InflateSetDictionary(strm, here.Window, WINSIZE);

			// skip uncompressed bytes until offset reached, then satisfy request
			offset -= here.Output;
			strm.AvailIn = 0;
			// while skipping to offset
			skip = true;
			do
			{
				// define where to put uncompressed data, and how much
				if (offset > WINSIZE)
				{   
					// skip WINSIZE bytes
					strm.AvailOut = WINSIZE;
					strm.NextOut = discard;
					offset -= WINSIZE;
				}
				else if (offset > 0)
				{   // last skip
					strm.AvailOut = (uint)offset;
					strm.NextOut = discard;
					offset = 0;
				}
				else if (skip)
				{   
					// at offset now
					strm.AvailOut = (uint)len;
					strm.NextOut = buf;
					// only do this once
					skip = false;
				}

				// uncompress until avail_out filled, or end of stream
				do
				{
					if (strm.AvailIn == 0)
					{
						strm.AvailIn = (uint)file.Read(input, 0, (int)CHUNK);
						if (strm.AvailIn == 0)
						{
							throw new ZException(ZResult.DATA_ERROR);
						}
						strm.NextIn = input;
					}
					ret = Inflate(strm, ZFlush.NO_FLUSH);
					// normal inflate
					if (ret == ZResult.MEM_ERROR || ret == ZResult.DATA_ERROR || ret == ZResult.NEED_DICT)
						throw new ZException(ret);
					if (ret == ZResult.STREAM_END)
					{
						// near the end of a gzip member, which might be followed by
						// another gzip member -- skip the gzip trailer and see if
						// there is more input after it
						if (strm.AvailIn < 8)
						{
							file.Seek(8 - strm.AvailIn, SeekOrigin.Current);
							strm.AvailIn = 0;
						}
						
						if (strm.AvailIn == 0 && file.Position == file.Length)
							// the input ended after the gzip trailer -- done
							break;

						// there is more input, so another gzip member should follow --
						// validate and skip the gzip header
						ret = InflateReset(strm, 31);
						if (ret != ZResult.OK)
							throw new ZException(ret);
						do
						{
							if (strm.AvailIn == 0)
							{
								strm.AvailIn = (uint)file.Read(input, 0, (int)CHUNK);
								// if (ferror(@in) != 0)
								// {
								// 	ret = ZResult.ERRNO;
								// 	throw new ZException(ZResult.ERRNO);
								// }
								if (strm.AvailIn == 0)
								{
									ret = ZResult.DATA_ERROR;
									throw new ZException(ZResult.DATA_ERROR);
								}
								strm.NextIn = input;
							}
							ret = Inflate(strm, ZFlush.BLOCK);
							if (ret == ZResult.MEM_ERROR || ret == ZResult.DATA_ERROR)
							{
								InflateEnd(strm);
								return value;
							}
						} while ((strm.DataType & 128) == 0);

						// set up to continue decompression of the raw deflate stream
						// that follows the gzip header
						ret = InflateReset(strm, -15);
						if (ret != ZResult.OK)
							throw new ZException(ret);
					}

					// continue to process the available input before reading more
				} while (strm.AvailOut != 0);

				if (ret == ZResult.STREAM_END)
					// reached the end of the compressed data -- return the data that
					// was available, possibly less than requested
					break;

				// do until offset reached and requested data read
			} while (skip);

			// compute the number of uncompressed bytes read after the offset
			value = skip ? 0 : len - (int)strm.AvailOut;

			return value;
		}
		finally
		{
			// clean up and return the bytes read, or the negative error
			InflateEnd(strm);
		}
	}

	public static int ExtractDeflateIndexM(
		FileStream file, Index index, long offset, byte[] bufs, int len)
	{
		// no need to pin (I guess); it's an unmanaged struct on stack
		ZStream strm = new();
		using var ms = new MemoryStream();
		byte[] input = new byte[CHUNK];
		byte[] output = new byte[WINSIZE];
		byte[] discard = new byte[WINSIZE];

		try
		{
			ZResult ret;
			int value = 0;
			bool skip;
			Point here;
			var streamOffset = 0;

			// proceed only if something reasonable to do
			if (len < 0)
				return 0;

			// find where in stream to start
			value = index.List.Count;
			while (--value != 0 && index.List[streamOffset + 1].Output <= offset)
				streamOffset++;
			here = index.List[streamOffset];

			// raw inflate
			ret = InflateInit(strm, -15);
			if (ret != ZResult.OK)
				throw new ZException(ret);
			// file.Seek(here.Input - (here.Bits != 0 ? 1 : 0), SeekOrigin.Begin);
			file.Position = here.Input;
			if (here.Bits != 0)
			{
				ret = (ZResult)file.ReadByte();
				if (ret == ZResult.ERRNO)
				{
					throw new ZException(ZResult.DATA_ERROR);
				}
				InflatePrime(strm, here.Bits, value >> (8 - here.Bits));
			}
			InflateSetDictionary(strm, here.Window, WINSIZE);

			// skip uncompressed bytes until offset reached, then satisfy request
			offset -= here.Output;
			strm.AvailIn = 0;
			// while skipping to offset
			skip = true;
			do
			{
				// define where to put uncompressed data, and how much
				if (offset > WINSIZE)
				{   
					// skip WINSIZE bytes
					strm.AvailOut = WINSIZE;
					strm.NextOut = discard;
					offset -= WINSIZE;
				}
				else if (offset > 0)
				{   // last skip
					strm.AvailOut = (uint)offset;
					strm.NextOut = discard;
					offset = 0;
				}
				else if (skip)
				{   
					// at offset now
					strm.AvailOut = (uint)len;
					strm.NextOut = output;
					// only do this once
					skip = false;
				}

				// uncompress until avail_out filled, or end of stream
				do
				{
					if (strm.AvailIn == 0)
					{
						strm.AvailIn = (uint)file.Read(input, 0, (int)CHUNK);
						if (strm.AvailIn == 0)
						{
							throw new ZException(ZResult.DATA_ERROR);
						}
						strm.NextIn = input;
					}
					ret = Inflate(strm, ZFlush.NO_FLUSH);
					ms.Write(bufs);
					// normal inflate
					if (ret == ZResult.MEM_ERROR || ret == ZResult.DATA_ERROR || ret == ZResult.NEED_DICT)
						throw new ZException(ret);
					if (ret == ZResult.STREAM_END)
					{
						// near the end of a gzip member, which might be followed by
						// another gzip member -- skip the gzip trailer and see if
						// there is more input after it
						if (strm.AvailIn < 8)
						{
							file.Seek(8 - strm.AvailIn, SeekOrigin.Current);
							strm.AvailIn = 0;
						}
						
						if (strm.AvailIn == 0 && file.Position == file.Length)
							// the input ended after the gzip trailer -- done
							break;

						// there is more input, so another gzip member should follow --
						// validate and skip the gzip header
						ret = InflateReset(strm, 31);
						if (ret != ZResult.OK)
							throw new ZException(ret);
						do
						{
							if (strm.AvailIn == 0)
							{
								strm.AvailIn = (uint)file.Read(input, 0, (int)CHUNK);
								// if (ferror(@in) != 0)
								// {
								// 	ret = ZResult.ERRNO;
								// 	throw new ZException(ZResult.ERRNO);
								// }
								if (strm.AvailIn == 0)
								{
									ret = ZResult.DATA_ERROR;
									throw new ZException(ZResult.DATA_ERROR);
								}
								strm.NextIn = input;
							}
							ret = Inflate(strm, ZFlush.BLOCK);
							if (ret == ZResult.MEM_ERROR || ret == ZResult.DATA_ERROR)
							{
								InflateEnd(strm);
								return value;
							}
						} while ((strm.DataType & 128) == 0);

						// set up to continue decompression of the raw deflate stream
						// that follows the gzip header
						ret = InflateReset(strm, -15);
						if (ret != ZResult.OK)
							throw new ZException(ret);
					}

					// continue to process the available input before reading more
				} while (strm.AvailOut != 0);

				if (ret == ZResult.STREAM_END)
					// reached the end of the compressed data -- return the data that
					// was available, possibly less than requested
					break;

				// do until offset reached and requested data read
			} while (skip);

			// compute the number of uncompressed bytes read after the offset
			value = skip ? 0 : len - (int)strm.AvailOut;

			return value;
		}
		finally
		{
			// clean up and return the bytes read, or the negative error
			InflateEnd(strm);
		}
	}
}
