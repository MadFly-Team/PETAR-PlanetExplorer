```

BenchmarkDotNet v0.15.2, Windows 11 (10.0.26200.8117)
Intel Core i9-10900KF CPU 3.70GHz, 1 CPU, 20 logical and 10 physical cores
.NET SDK 10.0.201
  [Host]     : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2
  Job-CNUJVU : .NET 8.0.25 (8.0.2526.11203), X64 RyuJIT AVX2

InvocationCount=1  UnrollFactor=1  

```
| Method                          | Mean     | Error     | StdDev    |
|-------------------------------- |---------:|----------:|----------:|
| HighAltitudeWaterCacheMissFrame | 1.809 ms | 0.0358 ms | 0.0912 ms |
