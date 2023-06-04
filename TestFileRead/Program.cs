using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;

Console.WriteLine("Hello, World!");

var num = 8;
var fss = new FileStream[num];
for (int i = 0; i < num; i++)
{
	fss[i] = File.OpenRead("../Benchmark/Samples/6144000.gz");
}

var buf = new byte[1024 * 1024];
var sw = new Stopwatch();
sw.Start();
for (int i = 0; i < 1000; i++)
{
	// Parallel.For(0, num, i => {
	// 	fss[i].Read(buf, 0, 1024 * 1024);
	// });
	fss[0].Read(buf, 0, 1024 * 1024);
}
sw.Stop();
Console.WriteLine(sw.ElapsedMilliseconds);