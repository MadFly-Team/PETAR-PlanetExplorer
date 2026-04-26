using System;
using System.Collections;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using Microsoft.Xna.Framework;
using Microsoft.VSDiagnostics;

namespace PEPAR.Benchmarks;
[CPUUsageDiagnoser]
public class WormsTerrainExplosionBenchmarks
{
    private object _drawer = null!;
    private MethodInfo _buildChunkPixelBufferMethod = null!;
    private MethodInfo _buildOverviewPixelsMethod = null!;
    private IList _terrainBlasts = null!;
    private IList _terrainDeposits = null!;
    private Type _terrainBlastType = null!;
    private Type _terrainDepositType = null!;
    [GlobalSetup]
    public void Setup()
    {
        var assembly = typeof(PEPAR.PEPAR_Ship).Assembly;
        var drawerType = assembly.GetType("PEPAR.Modules.Game.WormsTerrainDrawer", throwOnError: true)!;
        _drawer = Activator.CreateInstance(drawerType, nonPublic: true)!;
        drawerType.GetProperty("MapWidth", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(_drawer, 640 * 80);
        drawerType.GetProperty("MapHeight", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(_drawer, 400 * 12);
        drawerType.GetField("_chunkSize", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_drawer, 256);
        drawerType.GetField("_seed", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_drawer, 1337);
        _buildChunkPixelBufferMethod = drawerType.GetMethod("BuildChunkPixelBuffer", BindingFlags.Instance | BindingFlags.NonPublic)!;
        _buildOverviewPixelsMethod = drawerType.GetMethod("BuildOverviewPixels", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        _terrainBlasts = (IList)drawerType.GetField("_terrainBlasts", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_drawer)!;
        _terrainDeposits = (IList)drawerType.GetField("_terrainDeposits", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_drawer)!;
        _terrainBlastType = drawerType.GetNestedType("TerrainBlast", BindingFlags.NonPublic)!;
        _terrainDepositType = drawerType.GetNestedType("TerrainDeposit", BindingFlags.NonPublic)!;
        SeedExplosionState();
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _terrainBlasts.Clear();
        _terrainDeposits.Clear();
        SeedExplosionState();
    }

    [Benchmark]
    public object BuildChunkPixelBufferAfterExplosion()
    {
        return _buildChunkPixelBufferMethod.Invoke(_drawer, new object[] { 8, 5 })!;
    }

    [Benchmark]
    public object BuildOverviewPixelsAfterExplosion()
    {
        return _buildOverviewPixelsMethod.Invoke(_drawer, new object[] { 320, 96 })!;
    }

    private void SeedExplosionState()
    {
        _terrainBlasts.Add(Activator.CreateInstance(_terrainBlastType, new Vector2(2190f, 2005f), 26f)!);
        for (var index = 0; index < 200; index++)
        {
            var angle = index * 0.31415927f;
            var radius = 3f + (index % 11);
            var x = 2190f + (MathF.Cos(angle) * radius * 2.2f);
            var y = 2005f + (MathF.Sin(angle) * radius * 1.6f);
            var color = (index % 3) switch
            {
                0 => new Color(121, 87, 52),
                1 => new Color(102, 72, 46),
                _ => new Color(84, 58, 38)};
            _terrainDeposits.Add(Activator.CreateInstance(_terrainDepositType, new Vector2(x, y), 1.5f, color)!);
        }
    }
}