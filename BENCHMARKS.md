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
| 1 | 2.479 ms | 1.481 ms | 0.60 | 0.148 | 0.148 |
| 2 | 2.482 ms | 1.316 ms | 0.53 | 0.148 | 0.153 |
| 4 | 2.531 ms | 1.118 ms | 0.44 | 0.148 | 0.162 |
| 8 | 2.523 ms | 960.80 μs | 0.38 | 0.148 | 0.178 |
| 16 | 2.547 ms | 745.40 μs | 0.29 | 0.148 | 0.205 |

### <100kb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------:|----------:|--------------:|----------------------:|------------:|----------------:|
| 1 | 14.772 ms | 9.837 ms | 0.67 | 0.122 | 0.122 |
| 2 | 14.264 ms | 8.743 ms | 0.61 | 0.122 | 0.124 |
| 4 | 13.882 ms | 7.627 ms | 0.55 | 0.122 | 0.127 |
| 8 | 13.912 ms | 7.038 ms | 0.51 | 0.122 | 0.132 |
| 16 | 14.453 ms | 6.302 ms | 0.44 | 0.122 | 0.148 |

### <1mb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------:|----------:|--------------:|----------------------:|------------:|----------------:|
| 1 | 66.359 ms | 51.982 ms | 0.78 | 0.117 | 0.115 |
| 2 | 65.072 ms | 44.187 ms | 0.68 | 0.117 | 0.116 |
| 4 | 65.679 ms | 40.008 ms | 0.61 | 0.117 | 0.118 |
| 8 | 64.909 ms | 35.503 ms | 0.55 | 0.117 | 0.120 |
| 16 | 66.766 ms | 31.122 ms | 0.47 | 0.117 | 0.128 |

### >1mb
| Accel | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------:|----------:|--------------:|----------------------:|------------:|----------------:|
| 1 | 69.546 ms | 54.611 ms | 0.79 | 0.121 | 0.119 |
| 2 | 69.858 ms | 48.135 ms | 0.69 | 0.121 | 0.120 |
| 4 | 68.273 ms | 41.656 ms | 0.61 | 0.121 | 0.121 |
| 8 | 68.041 ms | 36.919 ms | 0.54 | 0.121 | 0.124 |
| 16 | 70.427 ms | 34.266 ms | 0.49 | 0.121 | 0.128 |


## Silesia corpus (fast compression)
Source: `SilesiaCodecLevel0Benchmarks`.

### Acceleration 1
| File | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------|----------:|--------------:|----------------------:|------------:|----------------:|
| dickens | 51.319 ms | 39.105 ms | 0.76 | 0.631 | 0.696 |
| mozilla | 163.213 ms | 129.887 ms | 0.80 | 0.516 | 0.555 |
| mr | 32.600 ms | 21.047 ms | 0.65 | 0.546 | 0.605 |
| nci | 56.640 ms | 56.379 ms | 1.00 | 0.165 | 0.156 |
| ooffice | 25.737 ms | 15.641 ms | 0.61 | 0.705 | 0.770 |
| osdb | 37.447 ms | 29.251 ms | 0.78 | 0.521 | 0.539 |
| reymont | 28.759 ms | 27.272 ms | 0.95 | 0.480 | 0.475 |
| samba | 58.430 ms | 51.161 ms | 0.88 | 0.357 | 0.376 |
| sao | 32.055 ms | 15.443 ms | 0.48 | 0.936 | 0.964 |
| webster | 176.628 ms | 142.189 ms | 0.81 | 0.486 | 0.509 |
| x-ray | 15.339 ms | 1.493 ms | 0.10 | 0.990 | 1.004 |
| xml | 11.249 ms | 10.545 ms | 0.94 | 0.230 | 0.218 |

### Acceleration 2
| File | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------|----------:|--------------:|----------------------:|------------:|----------------:|
| dickens | 49.435 ms | 28.793 ms | 0.58 | 0.631 | 0.757 |
| mozilla | 155.573 ms | 108.448 ms | 0.70 | 0.516 | 0.567 |
| mr | 31.735 ms | 17.789 ms | 0.56 | 0.546 | 0.607 |
| nci | 54.728 ms | 50.750 ms | 0.93 | 0.165 | 0.161 |
| ooffice | 25.140 ms | 11.808 ms | 0.47 | 0.705 | 0.798 |
| osdb | 37.049 ms | 24.334 ms | 0.66 | 0.521 | 0.561 |
| reymont | 27.826 ms | 24.195 ms | 0.87 | 0.480 | 0.511 |
| samba | 57.379 ms | 44.051 ms | 0.77 | 0.357 | 0.400 |
| sao | 31.220 ms | 9.888 ms | 0.32 | 0.936 | 0.978 |
| webster | 167.903 ms | 120.748 ms | 0.72 | 0.486 | 0.546 |
| x-ray | 14.599 ms | 1.376 ms | 0.09 | 0.990 | 1.004 |
| xml | 10.946 ms | 9.298 ms | 0.85 | 0.230 | 0.231 |

### Acceleration 4
| File | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------|----------:|--------------:|----------------------:|------------:|----------------:|
| dickens | 48.975 ms | 20.658 ms | 0.42 | 0.631 | 0.839 |
| mozilla | 157.427 ms | 92.383 ms | 0.59 | 0.516 | 0.593 |
| mr | 31.894 ms | 13.803 ms | 0.43 | 0.546 | 0.638 |
| nci | 53.896 ms | 47.338 ms | 0.88 | 0.165 | 0.169 |
| ooffice | 24.819 ms | 8.589 ms | 0.35 | 0.705 | 0.835 |
| osdb | 37.311 ms | 21.502 ms | 0.58 | 0.521 | 0.604 |
| reymont | 28.177 ms | 22.903 ms | 0.81 | 0.480 | 0.568 |
| samba | 58.381 ms | 39.488 ms | 0.68 | 0.357 | 0.438 |
| sao | 33.286 ms | 5.739 ms | 0.17 | 0.936 | 0.996 |
| webster | 169.494 ms | 103.310 ms | 0.61 | 0.486 | 0.594 |
| x-ray | 14.420 ms | 1.370 ms | 0.10 | 0.990 | 1.004 |
| xml | 11.261 ms | 8.616 ms | 0.77 | 0.230 | 0.248 |

### Acceleration 8
| File | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------|----------:|--------------:|----------------------:|------------:|----------------:|
| dickens | 48.475 ms | 12.884 ms | 0.27 | 0.631 | 0.921 |
| mozilla | 157.187 ms | 75.861 ms | 0.48 | 0.516 | 0.639 |
| mr | 31.765 ms | 9.767 ms | 0.31 | 0.546 | 0.673 |
| nci | 53.951 ms | 46.203 ms | 0.86 | 0.165 | 0.182 |
| ooffice | 24.742 ms | 6.007 ms | 0.24 | 0.705 | 0.876 |
| osdb | 37.497 ms | 17.687 ms | 0.47 | 0.521 | 0.687 |
| reymont | 28.014 ms | 17.902 ms | 0.64 | 0.480 | 0.661 |
| samba | 57.662 ms | 32.772 ms | 0.57 | 0.357 | 0.500 |
| sao | 30.894 ms | 3.687 ms | 0.12 | 0.936 | 1.001 |
| webster | 164.929 ms | 85.185 ms | 0.52 | 0.486 | 0.658 |
| x-ray | 14.087 ms | 1.370 ms | 0.10 | 0.990 | 1.004 |
| xml | 10.928 ms | 7.949 ms | 0.73 | 0.230 | 0.277 |

### Acceleration 16
| File | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) | K4os CRatio | LZ4Sharp CRatio |
|------|----------:|--------------:|----------------------:|------------:|----------------:|
| dickens | 47.982 ms | 7.170 ms | 0.15 | 0.631 | 0.972 |
| mozilla | 154.465 ms | 53.476 ms | 0.35 | 0.516 | 0.733 |
| mr | 31.216 ms | 6.433 ms | 0.21 | 0.546 | 0.700 |
| nci | 53.589 ms | 42.988 ms | 0.80 | 0.165 | 0.213 |
| ooffice | 24.315 ms | 3.830 ms | 0.16 | 0.705 | 0.917 |
| osdb | 35.351 ms | 10.827 ms | 0.31 | 0.521 | 0.853 |
| reymont | 27.160 ms | 12.612 ms | 0.46 | 0.480 | 0.784 |
| samba | 56.800 ms | 25.099 ms | 0.44 | 0.357 | 0.578 |
| sao | 30.334 ms | 2.439 ms | 0.08 | 0.936 | 1.003 |
| webster | 165.009 ms | 64.752 ms | 0.39 | 0.486 | 0.729 |
| x-ray | 14.327 ms | 1.398 ms | 0.10 | 0.990 | 1.004 |
| xml | 10.769 ms | 6.636 ms | 0.62 | 0.230 | 0.320 |


## JSON dataset (decompression)
Source: `JsonBenchmarks` — decompresses 100 documents per call.

| Size | Level | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) |
|------|------:|----------:|--------------:|----------------------:|
| 1kb | 3 | 29.07 μs | 26.21 μs | 0.90 |
| 1kb | 6 | 29.40 μs | 26.19 μs | 0.89 |
| 1kb | 9 | 28.99 μs | 25.94 μs | 0.89 |
| 1kb | 12 | 28.79 μs | 26.07 μs | 0.91 |
| 7kb | 3 | 255.38 μs | 295.05 μs | 1.16 |
| 7kb | 6 | 258.82 μs | 279.40 μs | 1.08 |
| 7kb | 9 | 254.39 μs | 282.60 μs | 1.11 |
| 7kb | 12 | 260.77 μs | 292.77 μs | 1.12 |
| 16kb | 3 | 636.83 μs | 699.46 μs | 1.10 |
| 16kb | 6 | 624.64 μs | 684.46 μs | 1.10 |
| 16kb | 9 | 632.53 μs | 681.78 μs | 1.08 |
| 16kb | 12 | 660.91 μs | 694.72 μs | 1.05 |
| 72kb | 3 | 3.166 ms | 3.469 ms | 1.10 |
| 72kb | 6 | 3.090 ms | 3.321 ms | 1.07 |
| 72kb | 9 | 2.965 ms | 3.335 ms | 1.12 |
| 72kb | 12 | 3.252 ms | 3.328 ms | 1.02 |

## Streaming API (LZ4Stream)

Streaming compression and decompression results for **LZ4Sharp** vs **K4os.Compression.LZ4.Streams**.
Source: `StreamBenchmarks` (JSON dataset corpus, payload buckets: `<10kb`, `<100kb`, `<1mb`, `>1mb`).

### Compress
| Payload | Level | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) |
|---------|------:|----------:|--------------:|----------------------:|
| <10kb | -1 | 3.404 ms | 2.402 ms | 0.71 |
| <10kb | 3 | 12.397 ms | 6.455 ms | 0.52 |
| <10kb | 9 | 12.398 ms | 6.614 ms | 0.53 |
| <10kb | 12 | 48.307 ms | 6.688 ms | 0.14 |
| <100kb | -1 | 18.625 ms | 13.586 ms | 0.73 |
| <100kb | 3 | 68.846 ms | 47.920 ms | 0.70 |
| <100kb | 9 | 79.110 ms | 53.876 ms | 0.68 |
| <100kb | 12 | 185.310 ms | 54.592 ms | 0.29 |
| <1mb | -1 | 83.977 ms | 61.612 ms | 0.73 |
| <1mb | 3 | 536.942 ms | 283.191 ms | 0.53 |
| <1mb | 9 | 919.157 ms | 436.083 ms | 0.47 |
| <1mb | 12 | 1.94 s | 442.033 ms | 0.23 |
| >1mb | -1 | 90.954 ms | 62.980 ms | 0.69 |
| >1mb | 3 | 575.250 ms | 297.960 ms | 0.52 |
| >1mb | 9 | 1.05 s | 449.121 ms | 0.43 |
| >1mb | 12 | 2.18 s | 469.493 ms | 0.22 |

### Decompress
| Payload | Level | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) |
|---------|------:|----------:|--------------:|----------------------:|
| <10kb | -1 | 2.809 ms | 1.961 ms | 0.70 |
| <10kb | 3 | 2.818 ms | 1.938 ms | 0.69 |
| <10kb | 9 | 2.928 ms | 1.940 ms | 0.66 |
| <10kb | 12 | 2.811 ms | 2.045 ms | 0.73 |
| <100kb | -1 | 18.156 ms | 13.693 ms | 0.75 |
| <100kb | 3 | 17.836 ms | 13.575 ms | 0.76 |
| <100kb | 9 | 18.457 ms | 13.620 ms | 0.74 |
| <100kb | 12 | 18.375 ms | 13.799 ms | 0.75 |
| <1mb | -1 | 135.452 ms | 112.542 ms | 0.83 |
| <1mb | 3 | 140.987 ms | 109.627 ms | 0.78 |
| <1mb | 9 | 134.522 ms | 106.567 ms | 0.79 |
| <1mb | 12 | 140.533 ms | 118.577 ms | 0.84 |
| >1mb | -1 | 172.299 ms | 156.311 ms | 0.91 |
| >1mb | 3 | 183.883 ms | 166.175 ms | 0.90 |
| >1mb | 9 | 182.216 ms | 141.126 ms | 0.77 |
| >1mb | 12 | 168.812 ms | 158.351 ms | 0.94 |

### Roundtrip (compress + decompress)
| Payload | Level | K4os Mean | LZ4Sharp Mean | Ratio (LZ4Sharp/K4os) |
|---------|------:|----------:|--------------:|----------------------:|
| <10kb | -1 | 6.594 ms | 4.619 ms | 0.70 |
| <10kb | 3 | 15.799 ms | 8.983 ms | 0.57 |
| <10kb | 9 | 15.693 ms | 8.861 ms | 0.56 |
| <10kb | 12 | 52.066 ms | 9.004 ms | 0.17 |
| <100kb | -1 | 35.575 ms | 27.444 ms | 0.77 |
| <100kb | 3 | 85.972 ms | 61.986 ms | 0.72 |
| <100kb | 9 | 97.134 ms | 67.409 ms | 0.69 |
| <100kb | 12 | 203.449 ms | 68.606 ms | 0.34 |
| <1mb | -1 | 215.145 ms | 176.197 ms | 0.82 |
| <1mb | 3 | 664.470 ms | 401.367 ms | 0.60 |
| <1mb | 9 | 1.05 s | 555.799 ms | 0.53 |
| <1mb | 12 | 2.10 s | 565.098 ms | 0.27 |
| >1mb | -1 | 263.533 ms | 220.206 ms | 0.84 |
| >1mb | 3 | 752.695 ms | 451.490 ms | 0.60 |
| >1mb | 9 | 1.21 s | 601.028 ms | 0.50 |
| >1mb | 12 | 2.24 s | 640.106 ms | 0.29 |

## Summary
- **JSON dataset (fast compress)**: LZ4Sharp is **0.29–0.79×** K4os (21–71% faster) across all payload sizes. Zero managed allocations.
- **JSON dataset (decompress)**: LZ4Sharp is **0.89–1.16×** K4os. Faster for small payloads (1kb: 9-11% faster); within 8-16% for medium payloads (7-72kb).
- **Silesia corpus**:
  - At **Accel 1**, LZ4Sharp is faster (0.08–1.00 ratio) with competitive compression ratios.
  - At **higher acceleration**, LZ4Sharp gains massive speedups (e.g., up to ~12× faster at Accel 16) but trades off compression ratio, offering a flexible performance profile.
- **Streaming API**:
  - **Compress**: LZ4Sharp is **1.4–7.1× faster** (0.14–0.73 ratio). Advantage grows dramatically at HC levels. Frame API compresses directly into destination (zero intermediate copies).
  - **Decompress**: LZ4Sharp is **1.1–1.5× faster** (0.66–0.94 ratio). Direct-to-user-buffer optimization and CopyTo override eliminate intermediate copies.
  - **Roundtrip**: LZ4Sharp is **1.2–5.9× faster** end-to-end, dominated by the compression advantage.

Detailed BenchmarkDotNet outputs: `BenchmarkDotNet.Artifacts/results/`.
