
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text;
using ParallelParsing.ZRan.NET;
using Index = ParallelParsing.ZRan.NET.Index;

namespace ParallelParsing;

class BatchedFASTQ : IEnumerable<FASTQRecord>, IDisposable
{
	public BatchedFASTQ(string indexPath, string gzipPath, bool enableSsdOptimization)
		: this(IndexIO.Deserialize(indexPath), gzipPath, enableSsdOptimization) { }
	public BatchedFASTQ(Index index, string gzipPath, bool enableSsdOptimization)
	{
		_Enumerator = new Enumerator(index, gzipPath, enableSsdOptimization);
	}

	private Enumerator _Enumerator;

	public IEnumerator<FASTQRecord> GetEnumerator() => _Enumerator;
	IEnumerator IEnumerable.GetEnumerator() => _Enumerator;

	public void Dispose()
	{
		_Enumerator.Dispose();
	}

	private class Enumerator : IEnumerator<FASTQRecord>
	{
		public Enumerator(Index index, string gzipPath, bool enableSsdOptimization)
		{
			BufferPool = ArrayPool<byte>.Create(index.ChunkMaxBytes, 1024);
			_Reader = new LazyFileReader(index, gzipPath, BufferPool, enableSsdOptimization);
			RecordCache = new();
			_Index = index;
			_Reader = new(index, gzipPath, BufferPool, enableSsdOptimization);
			_Current = default;
		}
		// ~ 500 MB to 1 GB
		public const int RECORD_CACHE_MAX_LENGTH = 20000;
		public ArrayPool<byte> BufferPool;
		public ConcurrentQueue<FASTQRecord> RecordCache;
		public LazyFileReader _Reader;
		private Index _Index;
		private FASTQRecord _Current;
		public FASTQRecord Current => _Current;
		object IEnumerator.Current => this.Current;

		public void Dispose()
		{
			_Reader.Dispose();
		}

		public bool MoveNext()
		{
			Task populateCache;
			// Console.WriteLine(RecordCache.Count);
			if (RecordCache.Count <= RECORD_CACHE_MAX_LENGTH &&
				_Reader.TryGetNewPartition(out var entry))
			{
				populateCache = Task.Run(() => {
					(var from, var to, var inBuf) = entry;
					var buf = BufferPool.Rent(_Index.ChunkMaxBytes);
					Debug.ExtractDummyRange(inBuf, from, to, buf);
					var rs = FASTQRecord.Parse(buf);
					// if (rs.Count != 10000) Console.WriteLine(rs.Count);
					Array.Clear(buf);
					Array.Clear(inBuf);
					BufferPool.Return(buf);
					BufferPool.Return(inBuf);
					Parallel.ForEach(rs, (r, _) => RecordCache.Enqueue(r));
				});
			}
			else populateCache = Task.Run(() => { });

			if (RecordCache.TryDequeue(out var res))
			{
				// Console.WriteLine("consumed");
				_Current = res;
				return true;
			}
			else
			{
				populateCache.Wait();
				// Console.WriteLine("wait");
				if (RecordCache.TryDequeue(out res))
				{
					_Current = res;
					return true;
				}
				else return false;
			}
		}

		void IEnumerator.Reset() => throw new NotSupportedException();
	}
}