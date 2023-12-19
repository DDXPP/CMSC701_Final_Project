using static ParallelParsing.Common.Constants;

namespace ParallelParsing.Common;

public sealed class Index
{
	// allocated list of entries
	private List<Point> _List;
	public int ChunkMaxBytes;

	public Index()
	{
		_List = new List<Point>(8);
	}
	public Index(IEnumerable<Point> points)
	{
		_List = new(points);
	}

	public Point this[int index] => _List[index];
	public int Count => _List.Count;
	public void Add(Point p) => _List.Add(p);

	public void AddPoint(int bits, long input, long output, uint left, byte[] window, byte[] offset)
	{

		if (this.Count == 0)
		{
			this.ChunkMaxBytes = (int)output;
		}
		else 
		{
			int outputSize = (int)output - (int)this[this.Count - 1].Output;

			if (outputSize > this.ChunkMaxBytes)
				this.ChunkMaxBytes = outputSize;
		}
		
		Point next = new Point(output, input, bits);
		next.offset = offset;

		if (left != 0)
			Array.Copy(window, WINSIZE - left, next.Window, 0, left);
			
		if (left < WINSIZE)
			Array.Copy(window, 0, next.Window, left, WINSIZE - left);
		this.Add(next);
	}
}

public struct Point
{
	// corresponding offset in uncompressed data
	public readonly long Output;

	// offset in input file of first full byte
	public readonly long Input;

	// number of bits (1-7) from byte at in-1, or 0
	public readonly int Bits;

	// preceding 32K of uncompressed data
	public readonly byte[] Window;

	public byte[]? offset;

	public Point(long output, long input, int bits)
	{
		this.Output = output;
		this.Input = input;
		this.Bits = bits;
		this.Window = new byte[WINSIZE];
		this.offset = null;
	}

	internal Point(long output, long input, int bits, byte[] window, byte[] offset)
	: this(output, input, bits)
	{
		this.Window = window;
		this.offset = offset;
	}
}
