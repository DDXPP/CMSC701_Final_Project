using Zlib.Extended.Enumerations;
using static Zlib.Extended.Zlib;

namespace Zlib.Extended
{
    /// <summary>
    /// Provides for a default wrapper around managing a stream and performing inflate/deflate.
    /// </summary>
    public static class Compressor
    {
        /// <summary>
        /// Inflates compressed input.
        /// </summary>
        /// <param name="input">Byte array of compressed data.</param>
        /// <param name="output">Empty array to place decompressed data.</param>
        /// <returns></returns>
        public static ReturnCode Decompress(byte[] input, ref byte[] output)
        {
            var stream = new z_stream
            {
                in_buf = input,
                out_buf = output,
                avail_in = (uint)input.Length,
                avail_out = (uint)output.Length
            };

            Inflate.inflateInit(stream);
            return Inflate.inflate(stream, FlushValue.Z_FINISH);
        }
    }
}
