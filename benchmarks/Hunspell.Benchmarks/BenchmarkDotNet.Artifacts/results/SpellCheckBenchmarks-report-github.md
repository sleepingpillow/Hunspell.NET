```

BenchmarkDotNet v0.14.0, Windows 11 (10.0.26200.7171)
Intel Core i7-8650U CPU 1.90GHz (Kaby Lake R), 1 CPU, 8 logical and 4 physical cores
.NET SDK 9.0.306
  [Host]     : .NET 9.0.10 (9.0.1025.47515), X64 RyuJIT AVX2
  DefaultJob : .NET 9.0.10 (9.0.1025.47515), X64 RyuJIT AVX2


```
| Method                    | Mean         | Error       | StdDev     | Median       | Gen0     | Allocated |
|-------------------------- |-------------:|------------:|-----------:|-------------:|---------:|----------:|
| SpellCheck_CorrectWords   |     5.149 μs |   0.3842 μs |   1.090 μs |     4.849 μs |        - |         - |
| SpellCheck_IncorrectWords |   903.798 μs |  46.4192 μs | 133.930 μs |   881.298 μs |  89.8438 |  380496 B |
| Suggest_Corrections       | 5,337.391 μs | 333.7641 μs | 973.606 μs | 5,147.442 μs | 453.1250 | 1915776 B |
| LoadDictionary            |           NA |          NA |         NA |           NA |       NA |        NA |

Benchmarks with issues:
  SpellCheckBenchmarks.LoadDictionary: DefaultJob
