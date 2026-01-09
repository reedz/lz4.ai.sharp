using BenchmarkDotNet.Running;

namespace LZ4Sharp.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--profile")
            {
                ProfilingBenchmarks.RunProfiling();
                return;
            }
            
            if (args.Length > 0 && args[0] == "--profile-detailed")
            {
                ProfilingBenchmark.Run();
                return;
            }
            
            if (args.Length > 0 && args[0] == "--ratio")
            {
                CompressionRatioAnalysis.Run();
                return;
            }
            
            if (args.Length > 0 && args[0] == "--compat")
            {
                CrossCompatibilityTests.Run();
                return;
            }
            
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
