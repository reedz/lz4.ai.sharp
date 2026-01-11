#!/bin/bash

# SIMD Hashing Benchmark Comparison Script
# This script builds and benchmarks LZ4Sharp with and without SIMD hashing enabled

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BENCHMARK_DIR="$SCRIPT_DIR"
RESULTS_DIR="$SCRIPT_DIR/BenchmarkDotNet.Artifacts/results"

echo "=========================================="
echo "LZ4Sharp SIMD Hashing Benchmark Comparison"
echo "=========================================="
echo

# Clean previous results
echo "Cleaning previous build artifacts..."
dotnet clean -c Release > /dev/null 2>&1

# Build and run WITHOUT SIMD
echo "=========================================="
echo "1. Building WITHOUT SIMD hashing..."
echo "=========================================="
dotnet build -c Release > /dev/null 2>&1
echo "✓ Build completed (Standard version)"
echo

echo "Running benchmarks (Standard version)..."
dotnet run -c Release -- --filter "*SIMDHashingBenchmark*" --exporters json --job short

# Save results
if [ -d "$RESULTS_DIR" ]; then
    echo
    echo "Saving standard results..."
    cp "$RESULTS_DIR"/LZ4Sharp.Benchmarks.SIMDHashingBenchmark-report.json \
       "$RESULTS_DIR"/standard-version-report.json 2>/dev/null || true
fi

# Build and run WITH SIMD
echo
echo "=========================================="
echo "2. Building WITH SIMD hashing..."
echo "=========================================="
dotnet clean -c Release > /dev/null 2>&1
dotnet build -c Release /p:DefineConstants="LZ4_ENABLE_SIMD_HASHING" > /dev/null 2>&1
echo "✓ Build completed (SIMD version)"
echo

echo "Running benchmarks (SIMD version)..."
dotnet run -c Release -- --filter "*SIMDHashingBenchmark*" --exporters json --job short

# Save results
if [ -d "$RESULTS_DIR" ]; then
    echo
    echo "Saving SIMD results..."
    cp "$RESULTS_DIR"/LZ4Sharp.Benchmarks.SIMDHashingBenchmark-report.json \
       "$RESULTS_DIR"/simd-version-report.json 2>/dev/null || true
fi

echo
echo "=========================================="
echo "Benchmark Comparison Complete!"
echo "=========================================="
echo "Results are in: $RESULTS_DIR"
echo
echo "Standard version: standard-version-report.json"
echo "SIMD version: simd-version-report.json"
echo
echo "Compare the 'Mean' times to see the performance difference."
echo "Expected improvement with SIMD: 15-25% for compression"
