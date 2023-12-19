namespace ParallelParsing.Common;

public static class IndexIO
{
	// chunkMaxBytes : int | count : int | points : [(output : long) 
	// | (input : long) | (bits : int) | (winLen : int) | (win : [byte])]
	public static void Serialize(this Index index, string path)
	{
		using var stream = File.Create(path);
		using var bw = new BinaryWriter(stream);

		bw.Write(0);
		bw.Write(index.ChunkMaxBytes);

		bw.Write(index.Count);

		for (int i = 0; i < index.Count; i++)
		{
			bw.Write(index[i].Output);
			bw.Write(index[i].Input);
			bw.Write(index[i].Bits);
			bw.Write(index[i].Window.Length);
			bw.Write(index[i].Window);
			bw.Write(index[i].offset?.Length ?? 0);
			bw.Write(index[i].offset ?? Array.Empty<byte>());
		}
	}

	public static Index Deserialize(string path)
	{
		using var stream = File.OpenRead(path);
		using var br = new BinaryReader(stream);

		br.ReadInt32();
		var chunkMaxBytes = br.ReadInt32();

		var count = br.ReadInt32();
		var points = new Point[count];

		for (int i = 0; i < count; i++)
		{
			var output = br.ReadInt64();
			var input = br.ReadInt64();
			var bits = br.ReadInt32();
			var winLen = br.ReadInt32();
			var window = br.ReadBytes(winLen);
			var offsetLen = br.ReadInt32();
			var offset = br.ReadBytes(offsetLen);
			points[i] = new Point(output, input, bits, window, offset);
		}

		return new Index(points);
	}
}
