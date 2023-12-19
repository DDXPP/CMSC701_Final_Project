using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using ParallelParsing.Common;
using ParallelParsing.Interop;
using Index = ParallelParsing.Common.Index;

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
			RecordCache = new();
			_Index = index;
			_Reader = new(index, gzipPath, enableSsdOptimization);
			_Current = default;
			// an approximate estimate of the actively running tasks
			_Tasks = new(index.Count / 4);
		}
		public const int RECORD_CACHE_MAX_LENGTH = 20000;
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
		}

		public bool MoveNext()
		{
			_Current.Dispose();

			if (RecordCache.Count <= RECORD_CACHE_MAX_LENGTH)
			{
				if (_Reader.TryGetNewPartition(out var entry))
				{
					var populateCache = Task.Run(() => {
						IEnumerable<FastqRecord> rs;
						(var from, var to, var inBuf, var owner) = entry;
						var bufOwner = MemoryPool<byte>.Shared.Rent((int)(to.Output - from.Output));
						var buf = bufOwner.Memory;
						Core.ExtractDeflateIndex(inBuf, from, to, buf);
						rs = Parsing.Parse(new CombinedMemory(from.offset, buf));
						foreach (var r in rs) RecordCache.Enqueue(r);

						inBuf.Span.Clear();
						owner.Dispose();
						buf.Span.Clear();
						bufOwner.Dispose();
					})
					.ContinueWith(t => _Tasks.Remove(t));
					_Tasks.Add(populateCache);
				}
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