using BenchmarkDotNet.Running;

namespace BenchmarkSuite1
{
    /** Entry point for the BenchmarkDotNet suite. */
    internal class Program
    {
        /**
         * Runs every benchmark class in this assembly. Build in Release —
         * BenchmarkDotNet refuses to run against a Debug build.
         *
         * <param name="args">passed through to BenchmarkDotNet for filtering and options</param>
         */
        static void Main(string[] args)
        {
            var _ = BenchmarkRunner.Run(typeof(Program).Assembly);
        }
    }
}
