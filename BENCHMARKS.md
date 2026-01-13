# Benchmarks

Fast compression (LZ4 level 0) results for **LZ4Sharp** vs **K4os.Compression.LZ4**.

## Environment
```

BenchmarkDotNet v0.14.0, Debian GNU/Linux 12 (bookworm)
Intel N100, 4 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.100
  [Host]     : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2
  DefaultJob : .NET 10.0.0 (10.0.25.52411), X64 RyuJIT AVX2


```

## JSON dataset (fast compression)
Source: `JsonDatasetLevel0Benchmarks` (payload buckets: `<10kb`, `<100kb`, `<1mb`, `>1mb`).

### <10kb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | CRatio |
|------:|----------:|--------------:|----------------------:|------:|
| 1 | 2.203 ms | 1.389 ms | 0.63 | 0.148 |
| 2 | 2.204 ms | 1.148 ms | 0.52 | 0.153 |
| 4 | 2.192 ms | 989.6 μs | 0.45 | 0.161 |
| 8 | 2.172 ms | 833.6 μs | 0.38 | 0.178 |
| 16 | 2.176 ms | 634.5 μs | 0.29 | 0.205 |

### <100kb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | CRatio |
|------:|----------:|--------------:|----------------------:|------:|
| 1 | 12.892 ms | 9.608 ms | 0.75 | 0.122 |
| 2 | 13.546 ms | 8.233 ms | 0.61 | 0.124 |
| 4 | 13.232 ms | 7.014 ms | 0.53 | 0.127 |
| 8 | 13.869 ms | 6.327 ms | 0.46 | 0.132 |
| 16 | 12.455 ms | 5.575 ms | 0.45 | 0.148 |

### <1mb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | CRatio |
|------:|----------:|--------------:|----------------------:|------:|
| 1 | 60.668 ms | 45.883 ms | 0.76 | 0.115 |
| 2 | 60.926 ms | 38.692 ms | 0.64 | 0.116 |
| 4 | 58.983 ms | 34.160 ms | 0.58 | 0.118 |
| 8 | 62.061 ms | 30.553 ms | 0.49 | 0.120 |
| 16 | 60.174 ms | 28.757 ms | 0.48 | 0.128 |

### >1mb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | CRatio |
|------:|----------:|--------------:|----------------------:|------:|
| 1 | 63.443 ms | 48.909 ms | 0.77 | 0.119 |
| 2 | 60.606 ms | 41.099 ms | 0.68 | 0.120 |
| 4 | 65.257 ms | 38.634 ms | 0.59 | 0.121 |
| 8 | 61.386 ms | 33.670 ms | 0.55 | 0.124 |
| 16 | 61.708 ms | 30.181 ms | 0.49 | 0.128 |

## Silesia corpus (fast compression)
Source: `SilesiaCodecLevel0Benchmarks` (acceleration **1**).

| File | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | CRatio |
|------|----------:|--------------:|----------------------:|------:|
| dickens | 45.391 ms | 35.798 ms | 0.79 | 0.696 |
| mozilla | 145.126 ms | 115.879 ms | 0.80 | 0.555 |
| mr | 29.447 ms | 19.684 ms | 0.67 | 0.605 |
| nci | 50.727 ms | 50.547 ms | 1.00 | 0.156 |
| ooffice | 22.768 ms | 14.384 ms | 0.63 | 0.770 |
| osdb | 32.964 ms | 26.138 ms | 0.79 | 0.539 |
| reymont | 25.328 ms | 23.356 ms | 0.92 | 0.475 |
| samba | 52.920 ms | 45.020 ms | 0.85 | 0.376 |
| sao | 28.229 ms | 13.742 ms | 0.49 | 0.964 |
| webster | 152.099 ms | 124.971 ms | 0.82 | 0.509 |
| x-ray | 13.509 ms | 1.271 ms | 0.09 | 1.004 |
| xml | 9.901 ms | 8.653 ms | 0.87 | 0.218 |

## Summary
- JSON dataset: ratios **0.29–0.77** (speedup **1.3x–3.4x** vs K4os).
- Silesia (accel=1): ratios **0.09–1.00** (speedup **1.0x–11.1x** vs K4os); faster in **11/12** files.

Detailed BenchmarkDotNet outputs: `BenchmarkDotNet.Artifacts/results/`.
