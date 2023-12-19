
using System.Buffers;
using System.Text;
using Mem = System.Memory<byte>;

namespace ParallelParsing.Common;

public struct FastqRecord : IDisposable
{
	public FastqRecord(IMemoryOwner<byte> owner, Mem idn, Mem seq, Mem pls, Mem qlt)
	{
		Owner = owner;
		_Identifier = idn;
		_Sequence = seq;
		_Other = pls;
		_Quality = qlt;
	}

	public FastqRecord(string idn, string seq, string pls, string qlt)
	{
		_IdentifierString = idn;
		_SequenceString = seq;
		_OtherString = pls;
		_QualityString = qlt;
	}

	private Mem _Memory;
	private Mem _Identifier;
	private Mem _Sequence;
	private Mem _Other;
	private Mem _Quality;
	private IMemoryOwner<byte>? Owner;

	private string? _IdentifierString = null;
	public string Identifier
	{
		get
		{
			if (_IdentifierString == null)
				_IdentifierString = Encoding.ASCII.GetString(_Identifier.Span);

			return _IdentifierString;
		}
	}

	private string? _SequenceString = null;
	public string Sequence
	{
		get
		{
			if (_SequenceString == null) _SequenceString = Encoding.ASCII.GetString(_Sequence.Span);
			
			return _SequenceString;
		}
	}

	private string? _OtherString = null;
	public string Other
	{
		get
		{
			if (_OtherString == null) _OtherString = Encoding.ASCII.GetString(_Other.Span);
			
			return _OtherString;
		}
	}

	private string? _QualityString = null;
	public string Quality
	{
		get
		{
			if (_QualityString == null) _QualityString = Encoding.ASCII.GetString(_Quality.Span);
			
			return _QualityString;
		}
	}

	public void Dispose()
	{
		_Memory.Span.Clear();
		Owner?.Dispose();
	}
}