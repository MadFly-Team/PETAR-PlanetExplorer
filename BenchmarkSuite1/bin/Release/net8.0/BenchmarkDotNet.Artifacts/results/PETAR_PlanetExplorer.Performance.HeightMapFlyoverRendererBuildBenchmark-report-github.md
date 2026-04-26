```

BenchmarkDotNet v0.15.2, Windows 11 (10.0.26200.8117)
Intel Core i9-10900KF CPU 3.70GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2


```
| Method             | Mean     | Error    | StdDev   |
|------------------- |---------:|---------:|---------:|
| BuildVisibleChunks | 63.75 μs | 0.541 μs | 0.452 μs |
