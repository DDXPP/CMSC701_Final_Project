using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using ParallelParsing.ZRan.NET;
using Index = ParallelParsing.ZRan.NET.Index;
using System.IO.Compression;
using static ParallelParsing.ZRan.NET.Constants;
// using static ParallelParsing.ZRan.NET.Compat;
using SDebug = System.Diagnostics.Debug;
using static Zlib.Extended.Inflate;
using static Zlib.Extended.Zlib;


//Bug: when there are more than 93 points in index, it will stop running

// var testFile = "../Gzipped_FASTQ_Files/SRR11192680_original.fastq.gz";
var testFile = "../Benchmark/Samples/12288000.gz";
var fs = File.OpenRead(testFile);
// var index = Core.BuildDeflateIndex_NEW(fs, span: 32768, 200); //1048576L
// var index = Core.BuildDeflateIndex_OLD(fs, span: 32768); //1048576L
// var index = Core.BuildDeflateIndex(fs, chunksize: 1200);
// i.Serialize("../Gzipped_FASTQ_Files/test1.fastq.gzi");

// Console.WriteLine(Core.BuildDeflateIndex_NEW(fs, span: 32768, 20000).List.Count());
// chunsize 30,000 works for 12288000.gz

//-----------------------------------------------------------------------------------------------------------
int x = 37;

// fs.Position = 0;
// var from = index.List[x];
// // var to = index.List[x + 1];
// var to = new Point(0, fs.Length, 0);
// var outBuf = new byte[2_000_000]; // change size *****************************************
// Core.ExtractDeflateIndex_OLD(fs, index, from.Output, outBuf, 1_600_000);
// outBuf.PrintASCIIFirstAndLast(400);


// var x = 0;
// var len_in = index.List[x + 1].Input - index.List[x].Input + 1;
// var from = index.List[x];
// var to = index.List[x + 1];
// var len_out = (int)(to.Output - from.Output);
// var fileBuffer = new byte[len_in];
// fs.Position = from.Input - 1;
// fs.ReadExactly(fileBuffer, 0, (int)len_in);
// var outBuf = new byte[len_out]; // change size *****************************************
// Core.ExtractDeflateIndex(fileBuffer, from, to, outBuf);
//-----------------------------------------------------------------------------------------------------------
// for (int x = 0; x < index.List.Count - 1; x++)
// {
// 	var len_in = index.List[x + 1].Input - index.List[x].Input + 1;
// 	var from = index.List[x];
// 	var to = index.List[x + 1];
// 	var len_out = (int)(to.Output - from.Output);
// 	var fileBuffer = new byte[len_in];
// 	fs.Position = from.Input - 1;
// 	fs.ReadExactly(fileBuffer, 0, (int)len_in);
// 	var outBuf = new byte[len_out]; // change size *****************************************
// 	Core.ExtractDeflateIndex(fileBuffer, from, to, outBuf);
// 	// Core.ExtractDeflateIndex_OLD(fs, index, from.Output, outBuf, len_out);
//     // outBuf.PrintASCIIFirstAndLast(1500);


//     // index.List[x].offset.PrintASCII(index.List[x].offset.Length);
//     // Console.WriteLine("--------------------");
//     // outBuf.PrintASCII(1000);
//     // Console.WriteLine("||||||||||||||||||||");
// 	Console.WriteLine(x);
// }


//-----------------------------------------------------------------------------------------------------------
// int x = 1;

// // index.List[1].Input --;
// // index.List[1].Output = 100555;
// // index.List[1].Bits = 2;
// // var uncompressedFile = "../Gzipped_FASTQ_Files/SRR11192680.fastq";
// // var uncompressedFS = File.OpenRead(uncompressedFile);
// // uncompressedFS.Position = 100555 - 32768;
// // var newWindow = new byte[32768];
// // uncompressedFS.ReadExactly(newWindow, 0, 32768);
// // index.List[1].Window = newWindow;

// // newWindow.PrintASCII(32768);

// fs.Position = 0;
// var from = index.List[x];
// var to = index.List[x + 1];
// var outBuf = new byte[2_000_000]; // change size *****************************************
// Core.ExtractDeflateIndex_OLD(fs, index, from.Output, outBuf, (int)(to.Output - from.Output));
// outBuf.PrintASCII(2000000);

//-----------------------------------------------------------------------------------------------------------
// Test if changing input offset or bits can solve the issue

// int x = 1;

// var originalInputValue = index.List[1].Input;


// var uncompressedFile = "../Gzipped_FASTQ_Files/SRR11192680.fastq";
// var uncompressedFS = File.OpenRead(uncompressedFile);
// // uncompressedFS.Position = index.List[1].Output - 32768;
// // var newWindow = new byte[32768];
// // uncompressedFS.ReadExactly(newWindow, 0, 32768);
// // newWindow.PrintASCII(32768);

// // index.List[1].Window.PrintASCII(32768);

// for (int inputOffset = -2000; inputOffset < 2000; inputOffset++)
// {
//     for (int bits = 0; bits < 8; bits++)
//     {
//         // for (int windowOffset = -100; windowOffset < 100; windowOffset++)
//         // {
//             index.List[x].Input = originalInputValue + inputOffset;
//             index.List[x].Bits = bits;

//             // uncompressedFS.Position = index.List[1].Output - 32768 + windowOffset;
//             // var newWindow = new byte[32768];
//             // uncompressedFS.ReadExactly(newWindow, 0, 32768);
//             // index.List[x].Window = newWindow;
//             // uncompressedFS.Position = 0;

//             fs.Position = 0;
//             var from = index.List[x];
//             var to = index.List[x + 1];
//             var outBuf = new byte[2_000_000]; 

//             try
//             {
//                 var returnValue = Core.ExtractDeflateIndex(fs, index, from.Output, outBuf, (int)(to.Output - from.Output));

//                 if (returnValue != -3)
//                 {                
//                     Console.WriteLine("input offset: " + inputOffset + ", bits: " + bits + 
//                     // ", windowOffset: " + windowOffset + 
//                     ", return value: " + (int)returnValue);
//                     // outBuf.PrintASCII(1000);
//                 }
//             }
//             catch (Exception e)
//             {
//                 // Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!");
//             }
//         // }
//     }
// }
















int xx = 0;











































// Core.ExtractDeflateIndex(fs, i, (int)i.List[1].Output, outBuf, (int)i.List[2].Output-(int)i.List[1].Output);
// Core.ExtractDeflateIndex(fs, i, (int)i.List[1].Output, outBuf, 500);


// Core.BuildDeflateIndex(fs, Constants.SPAN, 200);

// var fileName = "../Gzipped_FASTQ_Files/SRR11192680.fastq.gz";
// // var fileName = "../Gzipped_FASTQ_Files/tests/gplv3.txt.gz";
// using var file = File.OpenRead(fileName);
// var index = Core.BuildDeflateIndex(file, IndexIO.Deserialize(fileName).ChunkSize);
// // Console.WriteLine(len);

// const int LEN = 16384;
// byte[] buf = new byte[LEN];
// int ret;
// if (index != null)
// 	ret = Core.ExtractDeflateIndex(file, index, 200, buf, 400);
// Console.WriteLine(Encoding.ASCII.GetString(buf));

// for (int j = 1000; j < 10000; j += 200)
// {
//     var testFile1 = "../Gzipped_FASTQ_Files/SRR11192680_original.fastq.gz";
//     var fs1 = File.OpenRead(testFile1);
//     var i1 = Core.BuildDeflateIndex(fs1, (uint)j);
//     fs1.Dispose();
//     Console.WriteLine($"-------------finished building index with chunk size {j}.");
// }

// using var fs = File.OpenRead(testFile);
// using var ms = new MemoryStream();

ZResult ret;
uint have;
z_stream strm = new();
byte[] input = new byte[CHUNK];
byte[] output = new byte[CHUNK];

ret = (ZResult)inflateInit2(strm, 47);
SDebug.Assert(ret == ZResult.OK);

do
{
	strm.avail_in = (uint)fs.Read(input, 0, (int)CHUNK);

	if (strm.avail_in == 0) break;
	strm.in_buf = input;

	do
	{
		strm.avail_out = CHUNK;
		strm.out_buf = output;

		ret = (ZResult)inflate(strm, (int)ZFlush.NO_FLUSH);
		SDebug.Assert(ret != ZResult.DATA_ERROR);
		SDebug.Assert(ret != ZResult.STREAM_ERROR);

		have = CHUNK - strm.avail_out;
		// ms.Write(output, 0, (int)CHUNK);

	} while (strm.avail_out == 0);

} while (ret != ZResult.STREAM_END);

ret = (ZResult)inflateEnd(strm);

// var str = Encoding.ASCII.GetString(ms.GetBuffer());
// Console.WriteLine(str);