
using System.Buffers;
using System.Runtime.CompilerServices;
using ParallelParsing.Common;

namespace ParallelParsing;

public static class Parsing
{
	static object o = new object();
	public static IEnumerable<FastqRecord> Parse(CombinedMemory raw)
	{
		for (int i = 0; i < raw.Length;)
		{
			// empty space
			if (raw[i] == '\0') break;

			// skip @
			i++;
			var start = i;

			var idnFrom = i;
			var idnLen = ParseLine(ref i, raw) - 1;
			if (idnLen < 0) break;

			var seqFrom = i;
			var seqLen = ParseLine(ref i, raw) - 1;
			if (seqLen < 0) break;
			// skip +
			i++;

			var plsFrom = i;
			var plsLen = ParseLine(ref i, raw) - 1;
			if (plsLen < 0) break;

			var qltFrom = i;
			var qltLen = ParseLine(ref i, raw) - 1;
			if (qltLen < 0) break;
			var end = i;

			var owner = MemoryPool<byte>.Shared.Rent(end - start);
			var mem = owner.Memory;
			raw.CopyTo(start, end, mem);
			var idn = mem.Slice(idnFrom - start, idnLen);
			var seq = mem.Slice(seqFrom - start, seqLen);
			var pls = mem.Slice(plsFrom - start, plsLen);
			var qlt = mem.Slice(qltFrom - start, qltLen);

			yield return new FastqRecord(owner, idn, seq, pls, qlt);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int ParseLine(ref int pos, CombinedMemory raw)
	{
		var start = pos;
		while (true)
		{
			var b = raw[pos];
			if (b == '\n' || b == 0) break;
			else pos++;
		}

		if (raw[pos] == 0) return -1;

		// consume \n
		pos++;
		return pos - start;
	}
}

public struct CombinedMemory
{
	private Memory<byte> _Prepend;
	private int _LengthP;
	private Memory<byte> _Rest;
	public readonly int Length;

	public CombinedMemory(byte[]? prepend, Memory<byte> rest)
	{
		_Prepend = prepend;
		_Rest = rest;
		_LengthP = prepend?.Length ?? 0;
		Length = _LengthP + _Rest.Length;
	}

	public byte this[int i]
	{
		get
		{
			if (i < _LengthP) return _Prepend.Span[i];
			else return _Rest.Span[i - _LengthP];
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void CopyTo(int from, int to, Memory<byte> buf)
	{
		if (from < _LengthP && to < _LengthP)
		{
			var sub = _Prepend.Slice(from, to - from);
			sub.CopyTo(buf);
		}
		else if (from < _LengthP && to >= _LengthP)
		{
			var pre = _Prepend.Slice(from, _LengthP - from);
			var post = _Rest.Slice(0, to - _LengthP);
			pre.CopyTo(buf);
			post.CopyTo(buf.Slice(_LengthP - from));
		}
		else
		{
			var sub = _Rest.Slice(from - _LengthP, to - from);
			sub.CopyTo(buf);
		}
	}
}
