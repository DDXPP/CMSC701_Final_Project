
using ParallelParsing.Interop;
using Index = ParallelParsing.Common.Index;
using Point = ParallelParsing.Common.Point;
using System.Collections.Concurrent;
using System.Buffers;

namespace ParallelParsing;

public sealed class LazyFileReader : IDisposable
{
	public const int FILE_THREADS_COUNT_SSD = 8;
	public const int FILE_THREADS_COUNT_HDD = 1;
	public const int MAX_QUEUE_COUNT = 32;
	
	public readonly ConcurrentQueue<(Point from, Point to, Memory<byte> segment, IMemoryOwner<byte>)> PartitionQueue;
	private Index _Index;
	private FileStream[] _FileReads;
	private bool _IsEOF = false;
	private int _CurrPoint = 0;

	public LazyFileReader(Index index, string path, bool enableSsdOptimization)
	{
		_Index = index;
		PartitionQueue = new();		

		_FileReads = enableSsdOptimization ?
					   new FileStream[FILE_THREADS_COUNT_SSD] :
					   new FileStream[FILE_THREADS_COUNT_HDD];
		for (int i = 0; i < _FileReads.Length; i++)
		{
			_FileReads[i] = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
		}
	}

	public void Dispose()
	{
		Parallel.ForEach(_FileReads, f => f.Dispose());
	}

	private int TryReadMore()
	{
		Parallel.ForEach(_FileReads, fs => {
			if (_IsEOF) return;

			Point from;
			Point to;
			Memory<byte> buf;
			IMemoryOwner<byte> bufOwner;
			int len;
			lock (this)
			{
				from = _Index[_CurrPoint];

				_CurrPoint++;
				if (_CurrPoint < _Index.Count) to = _Index[_CurrPoint];
				else
				{
					_IsEOF = true;
					return;
				}

				len = (int)(to.Input - from.Input + 1);
			}
			bufOwner = MemoryPool<byte>.Shared.Rent(len);
			buf = bufOwner.Memory[..len];
			
			fs.Position = from.Input - 1;
			fs.Read(buf.Span);
			PartitionQueue.Enqueue((from, to, buf, bufOwner));
		});

		
		return 0;
	}

	public bool TryGetNewPartition(out (Point from, Point to, Memory<byte> segment, IMemoryOwner<byte>) entry)
	{
		if (_IsEOF && PartitionQueue.Count == 0)
		{
			entry = default;
			return false;
		}

		Task? readBytes = null;
		if (!_IsEOF && PartitionQueue.Count <= MAX_QUEUE_COUNT) readBytes = Task.Run(TryReadMore);

		if (PartitionQueue.TryDequeue(out entry))
		{
			return true;
		}
		else
		{
			readBytes?.Wait();
			return PartitionQueue.TryDequeue(out entry);
		}
	}
}
