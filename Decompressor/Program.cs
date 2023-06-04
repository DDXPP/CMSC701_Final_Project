using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Linq;
using System.Buffers;
using System.Diagnostics;
using Debug = ParallelParsing.ZRan.NET.Debug;
using ParallelParsing.ZRan.NET;

// test file downloaded from
// https://trace.ncbi.nlm.nih.gov/Traces/?view=run_browser&acc=SRR11192680
// https://trace.ncbi.nlm.nih.gov/Traces/?view=run_browser&acc=SRR21524988 

namespace ParallelParsing;

public class Program
{
    public static int readByteSize = 64;
    static void Main(string[] args)
    {
        // var gzipPath = "./../Gzipped_FASTQ_Files/SRR11192680_original.fastq.gz";
        var gzipPath = "./../Benchmark/Samples/768000.gz";
        // var gzipPath575 = "./../Gzipped_FASTQ_Files/SRR24554569_575.fastq";
        // var indexPath575 = "./../Gzipped_FASTQ_Files/SRR24554569_575.fastq.i";
        // var gzipPath32 = "./../Gzipped_FASTQ_Files/SRR24496856_32.fastq";
        // var indexPath32 = "./../Gzipped_FASTQ_Files/SRR24496856_32.fastq.i";
        // count = 588530
        // As = 37990794

        var sw = new Stopwatch();
        var fs = File.OpenRead(gzipPath);
        // sw.Start();
        var index = Core.BuildDeflateIndex(fs, 1200);
        // sw.Stop();
        Console.WriteLine("build index elapsed: " + sw.ElapsedMilliseconds);
        fs.Dispose();

        // fs = File.OpenRead(gzipPath);
        // var fastq = new BatchedFASTQ(index, gzipPath, false);
        // foreach (var r in fastq)
        // {
        //     r.ToString();
        // }
        // fs.Dispose();

        // var index = IndexIO.Deserialize(indexPath);
        // var count = 0;
        // for (int i = 0; i < index.List.Count - 1; i++)
        // {
        //     count += (int)index.ChunkSize;
        // }
        // Console.WriteLine(count);

        using var records = new BatchedFASTQ(index, gzipPath, enableSsdOptimization: true);
        sw.Start();
        // var count = records.Aggregate(0, (a, x) => a + x.Sequence.Count(c => c == 'A'));
        var count = records.Count();
        sw.Stop();
        Console.WriteLine(count);
        Console.WriteLine("Elapsed: " + sw.ElapsedMilliseconds);

        // var bytes = File.ReadAllBytes(gzipPath32);
        // sw.Start();
        // var res = FASTQRecord.Parse(bytes);
        // sw.Stop();
        // var count = 0;
        // // foreach (var r in res)
        // // {
        // //     count++;
        // // }
        // Console.WriteLine(res.Count());
        // // Console.WriteLine(res.Aggregate(0, (a, x) => { Thread.Sleep(1); return a + x.Sequence.Count(c => c == 'A'); }));
        // Console.WriteLine(sw.ElapsedMilliseconds);
    }
}







