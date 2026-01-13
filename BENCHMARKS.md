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
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------:|----------:|--------------:|----------------------:|------------:|----------------:|
| 1 | 2.203 ms | 1.389 ms | 0.63 | 0.148 | 0.148 |
| 2 | 2.204 ms | 1.148 ms | 0.52 | 0.148 | 0.153 |
| 4 | 2.192 ms | 989.6 μs | 0.45 | 0.148 | 0.161 |
| 8 | 2.172 ms | 833.6 μs | 0.38 | 0.148 | 0.178 |
| 16 | 2.176 ms | 634.5 μs | 0.29 | 0.148 | 0.205 |

### <100kb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------:|----------:|--------------:|----------------------:|------------:|----------------:|
| 1 | 12.892 ms | 9.608 ms | 0.75 | 0.122 | 0.122 |
| 2 | 13.546 ms | 8.233 ms | 0.61 | 0.122 | 0.124 |
| 4 | 13.232 ms | 7.014 ms | 0.53 | 0.122 | 0.127 |
| 8 | 13.869 ms | 6.327 ms | 0.46 | 0.122 | 0.132 |
| 16 | 12.455 ms | 5.575 ms | 0.45 | 0.122 | 0.148 |

### <1mb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------:|----------:|--------------:|----------------------:|------------:|----------------:|
| 1 | 60.668 ms | 45.883 ms | 0.76 | 0.117 | 0.115 |
| 2 | 60.926 ms | 38.692 ms | 0.64 | 0.117 | 0.116 |
| 4 | 58.983 ms | 34.160 ms | 0.58 | 0.117 | 0.118 |
| 8 | 62.061 ms | 30.553 ms | 0.49 | 0.117 | 0.120 |
| 16 | 60.174 ms | 28.757 ms | 0.48 | 0.117 | 0.128 |

### >1mb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------:|----------:|--------------:|----------------------:|------------:|----------------:|
| 1 | 63.443 ms | 48.909 ms | 0.77 | 0.121 | 0.119 |
| 2 | 60.606 ms | 41.099 ms | 0.68 | 0.121 | 0.120 |
| 4 | 65.257 ms | 38.634 ms | 0.59 | 0.121 | 0.121 |
| 8 | 61.386 ms | 33.670 ms | 0.55 | 0.121 | 0.124 |
| 16 | 61.708 ms | 30.181 ms | 0.49 | 0.121 | 0.128 |

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

## Summary
- **JSON dataset**: LZ4Sharp is significantly faster across all payload sizes (0.29–0.77 ratio).
- **Silesia corpus**:
  - At **Accel 1**, LZ4Sharp is faster (0.09–1.00 ratio) with competitive compression ratios.
  - At **higher acceleration**, LZ4Sharp gains massive speedups (e.g., up to ~10x faster at Accel 16) but trades off compression ratio, offering a flexible performance profile.

Detailed BenchmarkDotNet outputs: `BenchmarkDotNet.Artifacts/results/`.
