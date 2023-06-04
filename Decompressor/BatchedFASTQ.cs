
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Text;
using ParallelParsing.Common;
using ParallelParsing.ZRan.NET;
using Index = ParallelParsing.ZRan.NET.Index;

namespace ParallelParsing;

public sealed class BatchedFASTQ : IEnumerable<FastqRecord>, IDisposable
{
	public BatchedFASTQ(string indexPath, string gzipPath, bool enableSsdOptimization)
		: this(IndexIO.Deserialize(indexPath), gzipPath, enableSsdOptimization) { }
	public BatchedFASTQ(Index index, string gzipPath, bool enableSsdOptimization)
	{
		_Enumerator = new Enumerator(index, gzipPath, enableSsdOptimization);
	}

	private Enumerator _Enumerator;

	public IEnumerator<FastqRecord> GetEnumerator() => _Enumerator;
	IEnumerator IEnumerable.GetEnumerator() => _Enumerator;

	public void Dispose()
	{
		_Enumerator.Dispose();
	}

	private sealed class Enumerator : IEnumerator<FastqRecord>
	{
		public Enumerator(Index index, string gzipPath, bool enableSsdOptimization)
		{
			BufferPool = ArrayPool<byte>.Create(index.ChunkMaxBytes, 1024);
			_Tasks = new(index.Count / 2);
			_Reader = new LazyFileReader(index, gzipPath, BufferPool, _Tasks, enableSsdOptimization);
			RecordCache = new();
			_Index = index;
			// _Reader = new(index, gzipPath, BufferPool, _Tasks, enableSsdOptimization);
			_Current = default;
		}
		public const int RECORD_CACHE_MAX_LENGTH = 40000;
		public ArrayPool<byte> BufferPool;
		public ConcurrentQueue<FastqRecord> RecordCache;
		public LazyFileReader _Reader;
		private Index _Index;
		private FastqRecord _Current;
		public FastqRecord Current => _Current;
		object IEnumerator.Current => this.Current;
		private List<Task> _Tasks;

		public void Dispose()
		{
			_Reader.Dispose();
			// Console.WriteLine(FastqRecord.counter);
			// Console.WriteLine(counter);
		}

		public bool MoveNext()
		{
			// if (RecordCache.Count == 0) Console.WriteLine(RecordCache.Count);
			if (RecordCache.Count <= RECORD_CACHE_MAX_LENGTH &&
				_Reader.TryGetNewPartition(out var entry))
			{
				// var populateCache = Task.Run(() => {
					// semaphore.Wait();
					IEnumerable<FastqRecord> rs;
					(var from, var to, var inBuf, var owner) = entry;
					var buf = BufferPool.Rent((int)(to.Output - from.Output));
					Core.ExtractDeflateIndex(inBuf, from, to, buf);
					rs = Parsing.Parse(new CombinedMemory(from.offset, buf));
					foreach (var r in rs) RecordCache.Enqueue(r);
					Array.Clear(buf);
					inBuf.Span.Clear();
					owner.Dispose();
					BufferPool.Return(buf);
					// semaphore.Release();
				// }).ContinueWith(t => _Tasks.Remove(t));
				// _Tasks.Add(populateCache);
			}

			if (RecordCache.TryDequeue(out var res))
			{
				_Current = res;
				return true;
			}
			else
			{
				Task.WaitAll(_Tasks.ToArray());
				if (RecordCache.TryDequeue(out res))
				{
					_Current = res;
					return true;
				}
				else
				{
					return false;
				}
			}
		}

		void IEnumerator.Reset() => throw new NotSupportedException();
	}
}