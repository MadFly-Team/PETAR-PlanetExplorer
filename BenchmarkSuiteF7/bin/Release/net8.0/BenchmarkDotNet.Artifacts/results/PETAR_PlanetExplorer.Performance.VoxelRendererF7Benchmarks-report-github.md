```

BenchmarkDotNet v0.15.2, Windows 11 (10.0.26200.8328)
Intel Core i9-10900KF CPU 3.70GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.300-preview.0.26177.108
  [Host]     : .NET 8.0.26 (8.0.2626.16921), X64 RyuJIT AVX2
  Job-CNUJVU : .NET 8.0.26 (8.0.2626.16921), X64 RyuJIT AVX2

InvocationCount=1  UnrollFactor=1  

```
| Method                             | Mean     | Error   | StdDev  |
|----------------------------------- |---------:|--------:|--------:|
| WarmVisibleChunkMeshesAboveSurface | 114.6 ms | 2.24 ms | 3.74 ms |
