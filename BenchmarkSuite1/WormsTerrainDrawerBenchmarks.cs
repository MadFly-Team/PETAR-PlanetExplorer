using System;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

namespace PEPAR.Benchmarks;
[CPUUsageDiagnoser]
public class WormsTerrainDrawerBenchmarks
{
    private object _drawer = null!;
    private MethodInfo _buildChunkPixelBufferMethod = null!;
    private MethodInfo _stopChunkBuildWorkersMethod = null!;
    [GlobalSetup]
    public void Setup()
    {
        var assembly = typeof(PEPAR.PEPAR_Ship).Assembly;
        var drawerType = assembly.GetType("PEPAR.Modules.Game.WormsTerrainDrawer", throwOnError: true)!;
        _drawer = Activator.CreateInstance(drawerType, nonPublic: true)!;
        drawerType.GetProperty("MapWidth", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(_drawer, 640 * 80);
        drawerType.GetProperty("MapHeight", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(_drawer, 400 * 12);
        drawerType.GetField("_chunkSize", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_drawer, 256);
        drawerType.GetField("_seed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_drawer, 12345);
        _buildChunkPixelBufferMethod = drawerType.GetMethod("BuildChunkPixelBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!;
        _stopChunkBuildWorkersMethod = drawerType.GetMethod("StopChunkBuildWorkers", BindingFlags.Instance | BindingFlags.NonPublic)!;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _stopChunkBuildWorkersMethod.Invoke(_drawer, null);
    }

    [Benchmark]
    public object BuildChunkPixelBuffer()
    {
        return _buildChunkPixelBufferMethod.Invoke(_drawer, new object[] { 10, 5 })!;
    }
}