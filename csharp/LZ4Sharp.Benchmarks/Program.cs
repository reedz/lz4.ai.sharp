using BenchmarkDotNet.Running;

namespace LZ4Sharp.Benchmarks;

internal static class Program
{
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
}
