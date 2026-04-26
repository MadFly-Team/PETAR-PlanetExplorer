using BenchmarkDotNet.Running;
using PEPAR.Benchmarks;

namespace BenchmarkSuite1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(WormsTerrainDrawerBenchmarks).Assembly).Run(args);
        }
    }
}
