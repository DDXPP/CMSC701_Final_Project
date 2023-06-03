// compress.cs -- compress a memory buffer
// Copyright (C) 1995-2005 Jean-loup Gailly.
// Copyright (C) 2007-2011 by the Authors
// For conditions of distribution and use, see copyright notice in License.txt
// This file has been modified and does not represent the original software.

using Zlib.Extended.Enumerations;
using static Zlib.Extended.Zlib;

namespace Zlib.Extended
{
    public static class Compress
    {
        //   The following utility functions are implemented on top of the
        // basic stream-oriented functions. To simplify the interface, some
        // default options are assumed (compression level and memory usage,
        // standard memory allocation functions). The source code of these
        // utility functions can easily be modified if you need special options.

        // ===========================================================================
        // Compresses the source buffer into the destination buffer. The level
        // parameter has the same meaning as in deflateInit.  sourceLen is the byte
        // length of the source buffer. Upon entry, destLen is the total size of the
        // destination buffer, which must be at least 0.1% larger than sourceLen plus
        // 12 bytes. Upon exit, destLen is the actual size of the compressed buffer.
        //
        // compress2 returns Z_OK if success, Z_MEM_ERROR if there was not enough
        // memory, Z_BUF_ERROR if there was not enough room in the output buffer,
        // Z_STREAM_ERROR if the level parameter is invalid.

        public static ReturnCode Compress2(byte[] dest, ref uint destLen, byte[] source, uint sourceLen, int level)
        {
            z_stream stream = new()
            {
                next_in = 0,
                in_buf = source,
                avail_in = sourceLen,
                next_out = 0,
                out_buf = dest,
                avail_out = destLen
            };
            if (stream.avail_out != destLen) return ReturnCode.Z_BUF_ERROR;

            var err = Deflate.deflateInit(stream, level);
            if (err != ReturnCode.Z_OK) return err;

            err = Deflate.deflate(stream, FlushValue.Z_FINISH);
            if (err != ReturnCode.Z_STREAM_END)
            {
                Deflate.deflateEnd(stream);
                return err == ReturnCode.Z_OK ? ReturnCode.Z_BUF_ERROR : err;
            }
            destLen = stream.total_out;

            err = Deflate.deflateEnd(stream);
            return err;
        }

        // ===========================================================================
        //   Compresses the source buffer into the destination buffer.  sourceLen is
        // the byte length of the source buffer. Upon entry, destLen is the total
        // size of the destination buffer, which must be at least the value returned
        // by compressBound(sourceLen). Upon exit, destLen is the actual size of the
        // compressed buffer.

        //   This function can be used to compress a whole file at once if the
        // input file is mmap'ed.
        //   compress returns Z_OK if success, Z_MEM_ERROR if there was not
        // enough memory, Z_BUF_ERROR if there was not enough room in the output
        // buffer.

        public static ReturnCode compress(byte[] dest, ref uint destLen, byte[] source, uint sourceLen)
        {
            return Compress2(dest, ref destLen, source, sourceLen, Z_DEFAULT_COMPRESSION);
        }

        // ===========================================================================
        // If the default memLevel or windowBits for deflateInit() is changed, then
        // this function needs to be updated.

        //   compressBound() returns an upper bound on the compressed size after
        // compress() or compress2() on sourceLen bytes.  It would be used before
        // a compress() or compress2() call to allocate the destination buffer.

        public static uint CompressBound(uint sourceLen)
        {
            return sourceLen + (sourceLen >> 12) + (sourceLen >> 14) + (sourceLen >> 25) + 13;
        }
    }
}
