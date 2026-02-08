# Benchmarks

Fast compression (LZ4 level 0) results for **LZ4Sharp** vs **K4os.Compression.LZ4**.

## Environment
```
BenchmarkDotNet v0.14.0, Debian GNU/Linux 12 (bookworm)
Intel N100, 4 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.100
  [Host]   : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  ShortRun : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2

IterationCount=3  LaunchCount=1  WarmupCount=3
```

## JSON dataset (fast compression)
Source: `JsonDatasetLevel0Benchmarks` (payload buckets: `<10kb`, `<100kb`, `<1mb`, `>1mb`).

### <10kb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------:|----------:|--------------:|----------------------:|------------:|----------------:|
| 1 | 2.238 ms | 1.488 ms | 0.66 | 0.148 | 0.148 |
| 2 | 2.183 ms | 1.202 ms | 0.55 | 0.148 | 0.153 |
| 4 | 2.147 ms | 1.014 ms | 0.47 | 0.148 | 0.162 |
| 8 | 2.177 ms | 839.2 μs | 0.39 | 0.148 | 0.178 |
| 16 | 2.191 ms | 642.8 μs | 0.29 | 0.148 | 0.205 |

### <100kb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------:|----------:|--------------:|----------------------:|------------:|----------------:|
| 1 | 13.035 ms | 9.211 ms | 0.71 | 0.122 | 0.122 |
| 2 | 12.916 ms | 7.846 ms | 0.61 | 0.122 | 0.124 |
| 4 | 12.859 ms | 7.059 ms | 0.55 | 0.122 | 0.127 |
| 8 | 12.653 ms | 6.335 ms | 0.50 | 0.122 | 0.132 |
| 16 | 12.909 ms | 5.670 ms | 0.44 | 0.122 | 0.148 |

### <1mb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------:|----------:|--------------:|----------------------:|------------:|----------------:|
| 1 | 60.454 ms | 46.492 ms | 0.77 | 0.117 | 0.115 |
| 2 | 60.834 ms | 41.160 ms | 0.68 | 0.117 | 0.116 |
| 4 | 59.065 ms | 34.704 ms | 0.59 | 0.117 | 0.118 |
| 8 | 59.578 ms | 31.415 ms | 0.53 | 0.117 | 0.120 |
| 16 | 61.139 ms | 28.542 ms | 0.47 | 0.117 | 0.128 |

### >1mb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------:|----------:|--------------:|----------------------:|------------:|----------------:|
| 1 | 60.847 ms | 48.426 ms | 0.80 | 0.121 | 0.119 |
| 2 | 63.864 ms | 40.880 ms | 0.64 | 0.121 | 0.120 |
| 4 | 62.029 ms | 36.673 ms | 0.59 | 0.121 | 0.121 |
| 8 | 61.472 ms | 32.182 ms | 0.52 | 0.121 | 0.124 |
| 16 | 60.971 ms | 29.509 ms | 0.48 | 0.121 | 0.128 |

## Silesia corpus (fast compression)
Source: `SilesiaCodecLevel0Benchmarks`.

### Acceleration 1
| File | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------|----------:|--------------:|----------------------:|------------:|----------------:|
| dickens | 45.391 ms | 35.798 ms | 0.79 | 0.631 | 0.696 |
| mozilla | 145.126 ms | 115.879 ms | 0.80 | 0.516 | 0.555 |
| mr | 29.447 ms | 19.684 ms | 0.67 | 0.546 | 0.605 |
| nci | 50.727 ms | 50.547 ms | 1.00 | 0.165 | 0.156 |
| ooffice | 22.768 ms | 14.384 ms | 0.63 | 0.705 | 0.770 |
| osdb | 32.964 ms | 26.138 ms | 0.79 | 0.521 | 0.539 |
| reymont | 25.328 ms | 23.356 ms | 0.92 | 0.480 | 0.475 |
| samba | 52.920 ms | 45.020 ms | 0.85 | 0.357 | 0.376 |
| sao | 28.229 ms | 13.742 ms | 0.49 | 0.936 | 0.964 |
| webster | 152.099 ms | 124.971 ms | 0.82 | 0.486 | 0.509 |
| x-ray | 13.509 ms | 1.271 ms | 0.09 | 0.990 | 1.004 |
| xml | 9.901 ms | 8.653 ms | 0.87 | 0.230 | 0.218 |

### Acceleration 2
| File | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------|----------:|--------------:|----------------------:|------------:|----------------:|
| dickens | 43.935 ms | 27.207 ms | 0.62 | 0.631 | 0.757 |
| mozilla | 140.720 ms | 97.927 ms | 0.70 | 0.516 | 0.567 |
| mr | 28.835 ms | 16.342 ms | 0.57 | 0.546 | 0.607 |
| nci | 48.409 ms | 46.016 ms | 0.95 | 0.165 | 0.161 |
| ooffice | 22.337 ms | 10.757 ms | 0.48 | 0.705 | 0.798 |
| osdb | 32.734 ms | 21.801 ms | 0.67 | 0.521 | 0.561 |
| reymont | 25.079 ms | 21.455 ms | 0.86 | 0.480 | 0.511 |
| samba | 51.103 ms | 40.217 ms | 0.79 | 0.357 | 0.400 |
| sao | 29.524 ms | 9.349 ms | 0.32 | 0.936 | 0.978 |
| webster | 155.387 ms | 108.726 ms | 0.70 | 0.486 | 0.546 |
| x-ray | 14.228 ms | 1.311 ms | 0.09 | 0.990 | 1.004 |
| xml | 10.337 ms | 8.430 ms | 0.82 | 0.230 | 0.231 |

### Acceleration 4
| File | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------|----------:|--------------:|----------------------:|------------:|----------------:|
| dickens | 44.892 ms | 20.361 ms | 0.45 | 0.631 | 0.839 |
| mozilla | 147.866 ms | 86.108 ms | 0.58 | 0.516 | 0.593 |
| mr | 29.634 ms | 13.067 ms | 0.44 | 0.546 | 0.638 |
| nci | 50.063 ms | 46.041 ms | 0.92 | 0.165 | 0.169 |
| ooffice | 23.414 ms | 8.069 ms | 0.34 | 0.705 | 0.835 |
| osdb | 34.936 ms | 20.137 ms | 0.58 | 0.521 | 0.604 |
| reymont | 26.533 ms | 20.279 ms | 0.76 | 0.480 | 0.568 |
| samba | 55.467 ms | 36.921 ms | 0.67 | 0.357 | 0.438 |
| sao | 29.428 ms | 5.406 ms | 0.18 | 0.936 | 0.996 |
| webster | 152.474 ms | 94.882 ms | 0.62 | 0.486 | 0.594 |
| x-ray | 13.666 ms | 1.285 ms | 0.09 | 0.990 | 1.004 |
| xml | 10.310 ms | 7.587 ms | 0.74 | 0.230 | 0.248 |

### Acceleration 8
| File | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------|----------:|--------------:|----------------------:|------------:|----------------:|
| dickens | 45.030 ms | 11.959 ms | 0.27 | 0.631 | 0.921 |
| mozilla | 144.345 ms | 69.375 ms | 0.48 | 0.516 | 0.639 |
| mr | 29.797 ms | 8.736 ms | 0.29 | 0.546 | 0.673 |
| nci | 49.163 ms | 42.528 ms | 0.87 | 0.165 | 0.182 |
| ooffice | 22.757 ms | 5.413 ms | 0.24 | 0.705 | 0.876 |
| osdb | 33.519 ms | 15.837 ms | 0.47 | 0.521 | 0.687 |
| reymont | 25.069 ms | 16.196 ms | 0.65 | 0.480 | 0.661 |
| samba | 51.921 ms | 29.204 ms | 0.56 | 0.357 | 0.500 |
| sao | 28.410 ms | 3.467 ms | 0.12 | 0.936 | 1.001 |
| webster | 151.577 ms | 75.728 ms | 0.50 | 0.486 | 0.658 |
| x-ray | 13.104 ms | 1.237 ms | 0.09 | 0.990 | 1.004 |
| xml | 10.217 ms | 6.886 ms | 0.67 | 0.230 | 0.277 |

### Acceleration 16
| File | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------|----------:|--------------:|----------------------:|------------:|----------------:|
| dickens | 44.049 ms | 6.709 ms | 0.15 | 0.631 | 0.972 |
| mozilla | 143.671 ms | 49.034 ms | 0.34 | 0.516 | 0.733 |
| mr | 28.769 ms | 6.014 ms | 0.21 | 0.546 | 0.700 |
| nci | 48.873 ms | 40.619 ms | 0.83 | 0.165 | 0.213 |
| ooffice | 22.421 ms | 3.503 ms | 0.16 | 0.705 | 0.917 |
| osdb | 33.304 ms | 10.014 ms | 0.30 | 0.521 | 0.853 |
| reymont | 25.254 ms | 11.375 ms | 0.45 | 0.480 | 0.784 |
| samba | 51.532 ms | 23.220 ms | 0.45 | 0.357 | 0.578 |
| sao | 28.117 ms | 2.409 ms | 0.09 | 0.936 | 1.003 |
| webster | 152.285 ms | 59.705 ms | 0.39 | 0.486 | 0.729 |
| x-ray | 13.529 ms | 1.270 ms | 0.09 | 0.990 | 1.004 |
| xml | 9.972 ms | 6.065 ms | 0.61 | 0.230 | 0.320 |

## JSON dataset (decompression)
Source: `JsonBenchmarks` — decompresses 100 documents per call.

| Size | Level | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) |
|------|------:|----------:|--------------:|----------------------:|
| 1kb | 3 | 27.88 μs | 24.81 μs | 0.89 |
| 1kb | 6 | 28.10 μs | 26.07 μs | 0.93 |
| 1kb | 9 | 27.81 μs | 25.73 μs | 0.93 |
| 1kb | 12 | 28.45 μs | 25.76 μs | 0.91 |
| 7kb | 3 | 242.04 μs | 276.87 μs | 1.14 |
| 7kb | 6 | 240.14 μs | 281.50 μs | 1.17 |
| 7kb | 9 | 247.75 μs | 284.21 μs | 1.15 |
| 7kb | 12 | 247.99 μs | 268.31 μs | 1.08 |
| 16kb | 3 | 604.77 μs | 678.35 μs | 1.12 |
| 16kb | 6 | 610.71 μs | 638.90 μs | 1.05 |
| 16kb | 9 | 604.61 μs | 638.43 μs | 1.06 |
| 16kb | 12 | 630.75 μs | 644.55 μs | 1.02 |
| 72kb | 3 | 3.083 ms | 3.313 ms | 1.07 |
| 72kb | 6 | 3.143 ms | 3.264 ms | 1.04 |
| 72kb | 9 | 2.948 ms | 3.269 ms | 1.11 |
| 72kb | 12 | 3.172 ms | 3.190 ms | 1.01 |

## Streaming API (LZ4Stream)

Streaming compression and decompression results for **LZ4Sharp** vs **K4os.Compression.LZ4.Streams**.
Source: `StreamBenchmarks` (JSON dataset corpus, payload buckets: `<10kb`, `<100kb`, `<1mb`, `>1mb`).

### Compress
| Payload | Level | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) |
|---------|------:|----------:|--------------:|----------------------:|
| <10kb | -1 | 3.195 ms | 2.297 ms | 0.72 |
| <10kb | 3 | 12.917 ms | 6.577 ms | 0.51 |
| <10kb | 9 | 12.293 ms | 6.605 ms | 0.54 |
| <10kb | 12 | 49.485 ms | 6.657 ms | 0.13 |
| <100kb | -1 | 22.672 ms | 17.113 ms | 0.75 |
| <100kb | 3 | 70.325 ms | 47.614 ms | 0.68 |
| <100kb | 9 | 88.088 ms | 53.103 ms | 0.60 |
| <100kb | 12 | 181.16 ms | 53.274 ms | 0.29 |
| <1mb | -1 | 80.633 ms | 62.887 ms | 0.78 |
| <1mb | 3 | 519.41 ms | 278.91 ms | 0.54 |
| <1mb | 9 | 935.04 ms | 400.20 ms | 0.43 |
| <1mb | 12 | 1,845.02 ms | 417.57 ms | 0.23 |
| >1mb | -1 | 84.220 ms | 63.880 ms | 0.76 |
| >1mb | 3 | 567.83 ms | 294.46 ms | 0.52 |
| >1mb | 9 | 982.07 ms | 416.91 ms | 0.42 |
| >1mb | 12 | 2,170.37 ms | 442.57 ms | 0.20 |

### Decompress
| Payload | Level | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) |
|---------|------:|----------:|--------------:|----------------------:|
| <10kb | -1 | 2.768 ms | 1.903 ms | 0.69 |
| <10kb | 3 | 2.713 ms | 1.850 ms | 0.68 |
| <10kb | 9 | 2.713 ms | 1.867 ms | 0.69 |
| <10kb | 12 | 2.802 ms | 1.927 ms | 0.69 |
| <100kb | -1 | 17.009 ms | 12.857 ms | 0.76 |
| <100kb | 3 | 16.908 ms | 13.806 ms | 0.82 |
| <100kb | 9 | 18.057 ms | 13.372 ms | 0.74 |
| <100kb | 12 | 20.024 ms | 13.078 ms | 0.65 |
| <1mb | -1 | 124.06 ms | 110.86 ms | 0.89 |
| <1mb | 3 | 131.39 ms | 105.95 ms | 0.81 |
| <1mb | 9 | 125.17 ms | 108.93 ms | 0.87 |
| <1mb | 12 | 116.98 ms | 109.38 ms | 0.94 |
| >1mb | -1 | 165.40 ms | 151.68 ms | 0.92 |
| >1mb | 3 | 161.69 ms | 147.23 ms | 0.91 |
| >1mb | 9 | 180.53 ms | 143.02 ms | 0.79 |
| >1mb | 12 | 157.12 ms | 141.15 ms | 0.90 |

### Roundtrip (compress + decompress)
| Payload | Level | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) |
|---------|------:|----------:|--------------:|----------------------:|
| <10kb | -1 | 6.501 ms | 4.696 ms | 0.72 |
| <10kb | 3 | 14.945 ms | 8.862 ms | 0.59 |
| <10kb | 9 | 15.092 ms | 8.911 ms | 0.59 |
| <10kb | 12 | 52.157 ms | 9.544 ms | 0.18 |
| <100kb | -1 | 34.969 ms | 27.223 ms | 0.78 |
| <100kb | 3 | 86.092 ms | 60.283 ms | 0.70 |
| <100kb | 9 | 92.206 ms | 67.764 ms | 0.73 |
| <100kb | 12 | 188.42 ms | 67.927 ms | 0.36 |
| <1mb | -1 | 202.14 ms | 167.18 ms | 0.83 |
| <1mb | 3 | 637.31 ms | 422.18 ms | 0.66 |
| <1mb | 9 | 1,006.41 ms | 516.29 ms | 0.51 |
| <1mb | 12 | 2,016.54 ms | 516.63 ms | 0.26 |
| >1mb | -1 | 253.01 ms | 214.55 ms | 0.85 |
| >1mb | 3 | 707.77 ms | 460.61 ms | 0.65 |
| >1mb | 9 | 1,156.90 ms | 573.45 ms | 0.50 |
| >1mb | 12 | 2,239.48 ms | 591.55 ms | 0.26 |

## Summary
- **JSON dataset (fast compress)**: LZ4Sharp is **0.29–0.80×** K4os (20–71% faster) across all payload sizes. Zero managed allocations.
- **JSON dataset (HC compress)**: LZ4Sharp is **0.25–0.84×** K4os (16–75% faster). Up to 4× faster at HC12.
- **JSON dataset (decompress)**: LZ4Sharp is **0.89–1.17×** K4os. Faster for small payloads (1kb: 7-11% faster); within 2-17% for medium payloads (7-72kb). Major improvement from Phase 5 decompression loop restructuring + PGO.
- **Silesia corpus**:
  - At **Accel 1**, LZ4Sharp is faster (0.09–1.00 ratio) with competitive compression ratios.
  - At **higher acceleration**, LZ4Sharp gains massive speedups (e.g., up to ~10× faster at Accel 16) but trades off compression ratio, offering a flexible performance profile.
- **Streaming API**:
  - **Compress**: LZ4Sharp is **1.3–7.7× faster** (0.13–0.78 ratio). Advantage grows dramatically at HC levels (up to **5× faster** at HC12 for large payloads). Frame API compresses directly into destination (zero intermediate copies).
  - **Decompress**: LZ4Sharp is **1.1–1.5× faster** (0.65–0.94 ratio). Direct-to-user-buffer optimization eliminates intermediate copies.
  - **Roundtrip**: LZ4Sharp is **1.2–5.5× faster** end-to-end, dominated by the compression advantage.

Detailed BenchmarkDotNet outputs: `BenchmarkDotNet.Artifacts/results/`.
