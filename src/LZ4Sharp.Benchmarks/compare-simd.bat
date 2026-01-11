@echo off
REM SIMD Hashing Benchmark Comparison Script (Windows)
REM This script builds and benchmarks LZ4Sharp with and without SIMD hashing enabled

setlocal enabledelayedexpansion

echo ==========================================
echo LZ4Sharp SIMD Hashing Benchmark Comparison
echo ==========================================
echo.

REM Clean previous results
echo Cleaning previous build artifacts...
dotnet clean -c Release >nul 2>&1

REM Build and run WITHOUT SIMD
echo ==========================================
echo 1. Building WITHOUT SIMD hashing...
echo ==========================================
dotnet build -c Release >nul 2>&1
echo √ Build completed (Standard version)
echo.

echo Running benchmarks (Standard version)...
dotnet run -c Release -- --filter "*SIMDHashingBenchmark*" --exporters json --job short

echo.
echo Saving standard results...
if exist "BenchmarkDotNet.Artifacts\results\LZ4Sharp.Benchmarks.SIMDHashingBenchmark-report.json" (
    copy "BenchmarkDotNet.Artifacts\results\LZ4Sharp.Benchmarks.SIMDHashingBenchmark-report.json" ^
         "BenchmarkDotNet.Artifacts\results\standard-version-report.json" >nul 2>&1
)

REM Build and run WITH SIMD
echo.
echo ==========================================
echo 2. Building WITH SIMD hashing...
echo ==========================================
dotnet clean -c Release >nul 2>&1
dotnet build -c Release /p:DefineConstants="LZ4_ENABLE_SIMD_HASHING" >nul 2>&1
echo √ Build completed (SIMD version)
echo.

echo Running benchmarks (SIMD version)...
dotnet run -c Release -- --filter "*SIMDHashingBenchmark*" --exporters json --job short

echo.
echo Saving SIMD results...
if exist "BenchmarkDotNet.Artifacts\results\LZ4Sharp.Benchmarks.SIMDHashingBenchmark-report.json" (
    copy "BenchmarkDotNet.Artifacts\results\LZ4Sharp.Benchmarks.SIMDHashingBenchmark-report.json" ^
         "BenchmarkDotNet.Artifacts\results\simd-version-report.json" >nul 2>&1
)

echo.
echo ==========================================
echo Benchmark Comparison Complete!
echo ==========================================
echo Results are in: BenchmarkDotNet.Artifacts\results
echo.
echo Standard version: standard-version-report.json
echo SIMD version: simd-version-report.json
echo.
echo Compare the 'Mean' times to see the performance difference.
echo Expected improvement with SIMD: 15-25%% for compression

endlocal
