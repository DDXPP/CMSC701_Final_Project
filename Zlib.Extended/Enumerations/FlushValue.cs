namespace Zlib.Extended.Enumerations
{
    /// <summary>
    /// Controls how data should accumulate in memory before either compressing or decompressing.
    /// </summary>
    public enum FlushValue : int
    {
        Z_NO_FLUSH = 0,
        Z_PARTIAL_FLUSH = 1,
        Z_SYNC_FLUSH = 2,
        Z_FULL_FLUSH = 3,
        Z_FINISH = 4,
        Z_BLOCK = 5,
        Z_TREES = 6,

        /// <summary>
        /// Used to represent that a flush value was not specified or used.
        /// </summary>
        Z_INVALID = -1,
    }
}
