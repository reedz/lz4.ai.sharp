using BenchmarkDotNet.Running;

namespace LZ4Sharp.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            // Test corpus benchmarks if requested
            if (args.Length > 0 && args[0] == "--test-corpus")
            {
                CorpusTest.Run();
                return;
            }
            
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
