# SchedulerBenchmark

Tool-only BenchmarkDotNet lane for `HybridCpuLocalListScheduler`. It measures deterministic
straight-line scalar CIL corpora at 64/512/2048 operations. It is not referenced by production
Core/CIL/NativeAOT projects, does not establish backend legality, and must write BenchmarkDotNet
artifacts only below an explicitly selected `TempEnv` working/output directory.

Build does not execute the benchmark. Benchmark execution belongs to the single final validation
cycle and must use `--artifacts-path` under `TempEnv`.
