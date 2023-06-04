
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections;
using ParallelParsing.ZRan.NET;
using Index = ParallelParsing.ZRan.NET.Index;
using System.Collections.Concurrent;
using System.Buffers;
using System.Diagnostics;

namespace ParallelParsing;

public sealed class LazyFileReader : IDisposable
{
	public const int FILE_THREADS_COUNT_SSD = 2;
	public const int FILE_THREADS_COUNT_HDD = 1;
	
	public readonly ConcurrentQueue<(Point from, Point to, Memory<byte> segment, IMemoryOwner<byte>)> PartitionQueue;
	private Index _Index;
	// private IEnumerator<Point> _IndexEnumerator;
	private FileRead[] _FileReads;
	private ArrayPool<byte> _BufferPool;
	// private bool _IsEOF => _NumFinished == _FileReads.Length;
	private bool _IsEOF;
	private int _CurrPoint = 0;
	private int _NumFinished = 0;
	private MemoryStream ms;
	// private List<Task> tasks;

	public LazyFileReader(Index index, string path, ArrayPool<byte> pool, List<Task> tasks, bool enableSsdOptimization)
	{
		_Index = index;
		PartitionQueue = new();
		_BufferPool = pool;
		ms = new MemoryStream(File.ReadAllBytes(path));
		// this.tasks = tasks;
		// _IndexEnumerator = _Index.List.GetEnumerator();

		_FileReads = enableSsdOptimization ?
					   new FileRead[FILE_THREADS_COUNT_SSD] :
					   new FileRead[FILE_THREADS_COUNT_HDD];
		for (int i = 0; i < _FileReads.Length; i++)
		{
			_FileReads[i] = new FileRead(
				// File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read),
				ms,
				index.Count / _FileReads.Length * i,
				index.Count / _FileReads.Length * (i + 1)
			);
		}
	}

	public void Dispose()
	{
		Parallel.ForEach(_FileReads, f => f.FileStream.Dispose());
	}

	private void TryReadMore()
	{
		Parallel.ForEach(_FileReads, read => {
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
			buf = bufOwner.Memory.Slice(0, len);
			
			read.FileStream.Position = from.Input - 1;
			read.FileStream.Read(buf.Span);
			PartitionQueue.Enqueue((from, to, buf, bufOwner));
		});
	}
	
	// private void TryReadMore()
	// {
	// 	// tasks[0].Status
	// 	Parallel.ForEach(_FileReads, read => {
	// 		if (_IsEOF) return;

	// 		Point from;
	// 		Point to;
	// 		Memory<byte> buf;
	// 		IMemoryOwner<byte> bufOwner;
	// 		int len;

	// 		// from = _Index[0];
	// 		// to = _Index[^1];
	// 		// _NumFinished++;

	// 		from = _Index[read.CurrI];

	// 		read.CurrI++;
	// 		if (read.CurrI <= read.EndI) to = _Index[read.CurrI];
	// 		else
	// 		{
	// 			_NumFinished++;
	// 			return;
	// 		}

	// 		len = (int)(to.Input - from.Input + 1);
	// 		bufOwner = MemoryPool<byte>.Shared.Rent(len);
	// 		buf = bufOwner.Memory.Slice(0, len);
			
	// 		read.FileStream.Position = from.Input - 1;
	// 		read.FileStream.Read(buf.Span);
	// 		PartitionQueue.Enqueue((from, to, buf, bufOwner));
	// 	});
	// }

	public bool TryGetNewPartition(out (Point from, Point to, Memory<byte> segment, IMemoryOwner<byte>) entry)
	{
		if (_IsEOF && PartitionQueue.Count == 0)
		{
			entry = default;
			return false;
		}

		Task? readBytes = null;
		if (!_IsEOF && PartitionQueue.Count <= 32) readBytes = Task.Run(TryReadMore);

		if (PartitionQueue.TryDequeue(out entry))
		{
			return true;
		}
		else
		{
			Console.WriteLine("here");
			readBytes?.Wait();
			// if (_IsEOF && PartitionQueue.Count == 0) Console.WriteLine("here");
			return PartitionQueue.TryDequeue(out entry);
			// int prevCount = PartitionQueue.Count;
		}
	}

	private class FileRead
	{
		public FileRead(Stream fs, int curr, int end)
		{
			FileStream = fs;
			CurrI = curr;
			EndI = end;
		}
		public Stream FileStream;
		public int CurrI;
		public int EndI;
	}
}
