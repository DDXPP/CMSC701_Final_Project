﻿using System;
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
        var gzipPath575 = "./../Gzipped_FASTQ_Files/SRR24554569_575.fastq";
        var indexPath575 = "./../Gzipped_FASTQ_Files/SRR24554569_575.fastq.i";
        var gzipPath32 = "./../Gzipped_FASTQ_Files/SRR24496856_32.fastq";
        var indexPath32 = "./../Gzipped_FASTQ_Files/SRR24496856_32.fastq.i";
        // count = 588530
        // As = 37990794

        // var fs = File.OpenRead(gzipPath);
        // var index = Debug.BuildDummyIndex(fs, 10000);
        // index.Serialize("./../Gzipped_FASTQ_Files/SRR24554569_575.fastq.i");
        // Console.WriteLine(index.List.Count);
        // fs.Dispose();

        // fs = File.OpenRead(gzipPath);
        var sw = new Stopwatch();
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

        // using var records = new BatchedFASTQ(indexPath, gzipPath, enableSsdOptimization: true);
        // sw.Start();
        // var count = records.Aggregate(0, (a, x) => a + x.Sequence.Count(c => c == 'A'));
        // // var count = records.Count();
        // sw.Stop();
        // Console.WriteLine(count);
        // Console.WriteLine("Ellapsed: " + sw.ElapsedMilliseconds);

        var bytes = File.ReadAllBytes(gzipPath32);
        sw.Start();
        var res = FASTQRecord.Parse(bytes);
        sw.Stop();
        var count = 0;
        // foreach (var r in res)
        // {
        //     count++;
        // }
        Console.WriteLine(res.Count());
        // Console.WriteLine(res.Aggregate(0, (a, x) => { Thread.Sleep(1); return a + x.Sequence.Count(c => c == 'A'); }));
        Console.WriteLine(sw.ElapsedMilliseconds);
    }
}







