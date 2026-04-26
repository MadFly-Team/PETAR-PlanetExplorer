using System;
using Microsoft.Xna.Framework;

namespace PETAR_PlanetExplorer.Modules.Maps
{
    public sealed class PlanetTheme
    {
        private PlanetTheme(string key, string displayName, bool supportsLife, TerrainColorBand[] terrainBands, Color riverLowColor, Color riverHighColor, Color waterLowColor, Color waterHighColor)
        {
            Key = key;
            DisplayName = displayName;
            SupportsLife = supportsLife;
            TerrainBands = terrainBands;
            RiverLowColor = riverLowColor;
            RiverHighColor = riverHighColor;
            WaterLowColor = waterLowColor;
            WaterHighColor = waterHighColor;
        }

        public string Key { get; }

        public string DisplayName { get; }

        public bool SupportsLife { get; }

        public bool HasSurfaceWater => SupportsLife;

        public bool HasTrees => SupportsLife;

        public bool HasBirds => SupportsLife;

        public TerrainColorBand[] TerrainBands { get; }

        public Color RiverLowColor { get; }

        public Color RiverHighColor { get; }

        public Color WaterLowColor { get; }

        public Color WaterHighColor { get; }

        public static PlanetTheme Earth { get; } = new PlanetTheme(
            "earth",
            "Earth",
            supportsLife: true,
            terrainBands:
            [
                new TerrainColorBand(0f, ProceduralWorldMap.SeaLevel * 0.24f, new Color(2, 6, 26), new Color(6, 18, 58)),
                new TerrainColorBand(ProceduralWorldMap.SeaLevel * 0.24f, ProceduralWorldMap.SeaLevel * 0.5f, new Color(6, 18, 58), new Color(10, 44, 98)),
                new TerrainColorBand(ProceduralWorldMap.SeaLevel * 0.5f, ProceduralWorldMap.SeaLevel * 0.78f, new Color(10, 44, 98), new Color(22, 88, 156)),
                new TerrainColorBand(ProceduralWorldMap.SeaLevel * 0.78f, ProceduralWorldMap.SeaLevel, new Color(22, 88, 156), new Color(70, 170, 220)),
                new TerrainColorBand(ProceduralWorldMap.SeaLevel, ProceduralWorldMap.SeaLevel + 0.025f, new Color(168, 150, 98), new Color(198, 174, 120)),
                new TerrainColorBand(ProceduralWorldMap.SeaLevel + 0.025f, ProceduralWorldMap.SeaLevel + 0.055f, new Color(198, 174, 120), new Color(222, 206, 150)),
                new TerrainColorBand(ProceduralWorldMap.SeaLevel + 0.055f, 0.54f, new Color(58, 122, 70), new Color(82, 158, 82)),
                new TerrainColorBand(0.54f, 0.6f, new Color(82, 158, 82), new Color(114, 170, 92)),
                new TerrainColorBand(0.6f, 0.68f, new Color(114, 170, 92), new Color(148, 146, 92)),
                new TerrainColorBand(0.68f, 0.76f, new Color(148, 146, 92), new Color(132, 118, 98)),
                new TerrainColorBand(0.76f, 0.84f, new Color(132, 118, 98), new Color(164, 154, 154)),
                new TerrainColorBand(0.84f, 1f, new Color(164, 154, 154), Color.White)
            ],
            riverLowColor: new Color(18, 74, 150),
            riverHighColor: new Color(124, 232, 255),
            waterLowColor: new Color(12, 74, 86, 150),
            waterHighColor: new Color(56, 156, 150, 112));

        public static PlanetTheme Mercury { get; } = new PlanetTheme(
            "mercury",
            "Mercury",
            supportsLife: false,
            terrainBands:
            [
                new TerrainColorBand(0f, 0.12f, new Color(18, 18, 24), new Color(34, 34, 42)),
                new TerrainColorBand(0.12f, 0.28f, new Color(34, 34, 42), new Color(62, 58, 66)),
                new TerrainColorBand(0.28f, 0.48f, new Color(62, 58, 66), new Color(104, 92, 84)),
                new TerrainColorBand(0.48f, 0.68f, new Color(104, 92, 84), new Color(146, 132, 118)),
                new TerrainColorBand(0.68f, 0.86f, new Color(146, 132, 118), new Color(188, 176, 162)),
                new TerrainColorBand(0.86f, 1f, new Color(188, 176, 162), new Color(236, 228, 214))
            ],
            riverLowColor: Color.Transparent,
            riverHighColor: Color.Transparent,
            waterLowColor: Color.Transparent,
            waterHighColor: Color.Transparent);

        public static PlanetTheme Venus { get; } = new PlanetTheme(
            "venus",
            "Venus",
            supportsLife: false,
            terrainBands:
            [
                new TerrainColorBand(0f, 0.16f, new Color(52, 28, 18), new Color(82, 46, 26)),
                new TerrainColorBand(0.16f, 0.34f, new Color(82, 46, 26), new Color(126, 74, 36)),
                new TerrainColorBand(0.34f, 0.56f, new Color(126, 74, 36), new Color(176, 112, 48)),
                new TerrainColorBand(0.56f, 0.76f, new Color(176, 112, 48), new Color(214, 154, 74)),
                new TerrainColorBand(0.76f, 0.9f, new Color(214, 154, 74), new Color(232, 198, 128)),
                new TerrainColorBand(0.9f, 1f, new Color(232, 198, 128), new Color(248, 234, 192))
            ],
            riverLowColor: Color.Transparent,
            riverHighColor: Color.Transparent,
            waterLowColor: Color.Transparent,
            waterHighColor: Color.Transparent);

        public static PlanetTheme Mars { get; } = new PlanetTheme(
            "mars",
            "Mars",
            supportsLife: false,
            terrainBands:
            [
                new TerrainColorBand(0f, 0.14f, new Color(42, 18, 16), new Color(68, 24, 20)),
                new TerrainColorBand(0.14f, 0.32f, new Color(68, 24, 20), new Color(108, 38, 28)),
                new TerrainColorBand(0.32f, 0.54f, new Color(108, 38, 28), new Color(156, 56, 38)),
                new TerrainColorBand(0.54f, 0.74f, new Color(156, 56, 38), new Color(194, 86, 56)),
                new TerrainColorBand(0.74f, 0.9f, new Color(194, 86, 56), new Color(220, 134, 92)),
                new TerrainColorBand(0.9f, 1f, new Color(220, 134, 92), new Color(242, 188, 148))
            ],
            riverLowColor: Color.Transparent,
            riverHighColor: Color.Transparent,
            waterLowColor: Color.Transparent,
            waterHighColor: Color.Transparent);

        public static PlanetTheme Moon { get; } = new PlanetTheme(
            "moon",
            "Moon",
            supportsLife: false,
            terrainBands:
            [
                new TerrainColorBand(0f, 0.12f, new Color(18, 18, 20), new Color(32, 32, 36)),
                new TerrainColorBand(0.12f, 0.28f, new Color(32, 32, 36), new Color(60, 60, 66)),
                new TerrainColorBand(0.28f, 0.48f, new Color(60, 60, 66), new Color(102, 102, 110)),
                new TerrainColorBand(0.48f, 0.72f, new Color(102, 102, 110), new Color(150, 150, 156)),
                new TerrainColorBand(0.72f, 0.9f, new Color(150, 150, 156), new Color(202, 202, 206)),
                new TerrainColorBand(0.9f, 1f, new Color(202, 202, 206), new Color(244, 244, 246))
            ],
            riverLowColor: Color.Transparent,
            riverHighColor: Color.Transparent,
            waterLowColor: Color.Transparent,
            waterHighColor: Color.Transparent);

        public static PlanetTheme Aetheris { get; } = CreateRandomTheme("aetheris", "Aetheris", new Color(28, 196, 160), new Color(202, 96, 244));
        public static PlanetTheme Brontara { get; } = CreateRandomTheme("brontara", "Brontara", new Color(238, 108, 72), new Color(255, 218, 94));
        public static PlanetTheme Cryon { get; } = CreateRandomTheme("cryon", "Cryon", new Color(64, 168, 255), new Color(188, 248, 255));
        public static PlanetTheme Draxis { get; } = CreateRandomTheme("draxis", "Draxis", new Color(92, 72, 206), new Color(255, 112, 182));
        public static PlanetTheme Eryndor { get; } = CreateRandomTheme("eryndor", "Eryndor", new Color(44, 214, 118), new Color(246, 232, 104));
        public static PlanetTheme Fyra { get; } = CreateRandomTheme("fyra", "Fyra", new Color(255, 92, 58), new Color(255, 164, 220));
        public static PlanetTheme Galvion { get; } = CreateRandomTheme("galvion", "Galvion", new Color(88, 222, 255), new Color(84, 100, 232));
        public static PlanetTheme Heliora { get; } = CreateRandomTheme("heliora", "Heliora", new Color(255, 204, 84), new Color(255, 118, 42));
        public static PlanetTheme Ilyth { get; } = CreateRandomTheme("ilyth", "Ilyth", new Color(166, 90, 255), new Color(82, 248, 218));
        public static PlanetTheme Jorune { get; } = CreateRandomTheme("jorune", "Jorune", new Color(255, 126, 126), new Color(255, 234, 166));
        public static PlanetTheme Kaelion { get; } = CreateRandomTheme("kaelion", "Kaelion", new Color(36, 142, 255), new Color(50, 255, 152));
        public static PlanetTheme Lysara { get; } = CreateRandomTheme("lysara", "Lysara", new Color(244, 84, 190), new Color(134, 214, 255));
        public static PlanetTheme Myrr { get; } = CreateRandomTheme("myrr", "Myrr", new Color(132, 224, 96), new Color(36, 112, 54));
        public static PlanetTheme Novera { get; } = CreateRandomTheme("novera", "Novera", new Color(110, 122, 255), new Color(224, 114, 255));
        public static PlanetTheme Orinth { get; } = CreateRandomTheme("orinth", "Orinth", new Color(255, 156, 54), new Color(130, 72, 42));
        public static PlanetTheme Pyraxis { get; } = CreateRandomTheme("pyraxis", "Pyraxis", new Color(255, 60, 60), new Color(116, 8, 36));
        public static PlanetTheme Quoril { get; } = CreateRandomTheme("quoril", "Quoril", new Color(58, 238, 202), new Color(22, 88, 122));
        public static PlanetTheme Rimefall { get; } = CreateRandomTheme("rimefall", "Rimefall", new Color(206, 244, 255), new Color(120, 160, 255));
        public static PlanetTheme Solarax { get; } = CreateRandomTheme("solarax", "Solarax", new Color(255, 222, 116), new Color(255, 82, 126));
        public static PlanetTheme Thalor { get; } = CreateRandomTheme("thalor", "Thalor", new Color(86, 255, 206), new Color(16, 108, 132));
        public static PlanetTheme Umbrix { get; } = CreateRandomTheme("umbrix", "Umbrix", new Color(78, 66, 126), new Color(26, 18, 44));
        public static PlanetTheme Virelia { get; } = CreateRandomTheme("virelia", "Virelia", new Color(120, 255, 108), new Color(34, 148, 98));
        public static PlanetTheme Wyrmora { get; } = CreateRandomTheme("wyrmora", "Wyrmora", new Color(255, 98, 150), new Color(128, 20, 82));
        public static PlanetTheme Xantheos { get; } = CreateRandomTheme("xantheos", "Xantheos", new Color(255, 236, 90), new Color(102, 164, 20));
        public static PlanetTheme Ydris { get; } = CreateRandomTheme("ydris", "Ydris", new Color(78, 238, 255), new Color(166, 110, 255));
        public static PlanetTheme Zephyria { get; } = CreateRandomTheme("zephyria", "Zephyria", new Color(210, 210, 255), new Color(116, 168, 255));
        public static PlanetTheme Arcturon { get; } = CreateRandomTheme("arcturon", "Arcturon", new Color(88, 160, 255), new Color(28, 62, 132));
        public static PlanetTheme BorealisPrime { get; } = CreateRandomTheme("borealis-prime", "Borealis Prime", new Color(98, 255, 170), new Color(48, 112, 255));
        public static PlanetTheme Cindros { get; } = CreateRandomTheme("cindros", "Cindros", new Color(255, 138, 40), new Color(84, 34, 28));
        public static PlanetTheme Driftholm { get; } = CreateRandomTheme("driftholm", "Driftholm", new Color(164, 154, 255), new Color(82, 222, 220));
        public static PlanetTheme Emberreach { get; } = CreateRandomTheme("emberreach", "Emberreach", new Color(255, 94, 52), new Color(255, 202, 98));
        public static PlanetTheme Frostmere { get; } = CreateRandomTheme("frostmere", "Frostmere", new Color(202, 242, 255), new Color(94, 138, 212));
        public static PlanetTheme Gloomtide { get; } = CreateRandomTheme("gloomtide", "Gloomtide", new Color(42, 76, 122), new Color(24, 198, 180));
        public static PlanetTheme HyperionDelta { get; } = CreateRandomTheme("hyperion-delta", "Hyperion Delta", new Color(255, 182, 90), new Color(196, 72, 255));
        public static PlanetTheme Iridessa { get; } = CreateRandomTheme("iridessa", "Iridessa", new Color(255, 112, 224), new Color(110, 236, 255));

        public static PlanetTheme[] All { get; } =
        [
            Earth,
            Mercury,
            Venus,
            Mars,
            Moon,
            Aetheris,
            Brontara,
            Cryon,
            Draxis,
            Eryndor,
            Fyra,
            Galvion,
            Heliora,
            Ilyth,
            Jorune,
            Kaelion,
            Lysara,
            Myrr,
            Novera,
            Orinth,
            Pyraxis,
            Quoril,
            Rimefall,
            Solarax,
            Thalor,
            Umbrix,
            Virelia,
            Wyrmora,
            Xantheos,
            Ydris,
            Zephyria,
            Arcturon,
            BorealisPrime,
            Cindros,
            Driftholm,
            Emberreach,
            Frostmere,
            Gloomtide,
            HyperionDelta,
            Iridessa
        ];

        public static PlanetTheme GetByIndex(int index)
        {
            return All[Math.Clamp(index, 0, All.Length - 1)];
        }

        private static PlanetTheme CreateRandomTheme(string key, string displayName, Color primary, Color accent)
        {
            var seed = CombineThemeSeed(key, displayName, primary, accent);
            var primaryHue = GetNormalizedSeed(seed, 0);
            var contrastOffset = 0.42f + (GetNormalizedSeed(seed, 1) * 0.16f);
            var accentHue = WrapHue(primaryHue + contrastOffset);

            var primaryColor = FromHsv(
                primaryHue,
                0.72f + (GetNormalizedSeed(seed, 2) * 0.24f),
                0.8f + (GetNormalizedSeed(seed, 3) * 0.18f));
            var accentColor = FromHsv(
                accentHue,
                0.72f + (GetNormalizedSeed(seed, 4) * 0.24f),
                0.82f + (GetNormalizedSeed(seed, 5) * 0.16f));

            var deepPrimary = LerpColor(primaryColor, Color.Black, 0.78f);
            var lowPrimary = LerpColor(primaryColor, Color.Black, 0.42f);
            var midPrimary = LerpColor(primaryColor, Color.White, 0.08f);
            var highPrimary = LerpColor(primaryColor, Color.White, 0.36f);
            var lowAccent = LerpColor(accentColor, Color.Black, 0.42f);
            var midAccent = LerpColor(accentColor, Color.White, 0.08f);
            var highAccent = LerpColor(accentColor, Color.White, 0.36f);
            var peakPrimary = LerpColor(primaryColor, Color.White, 0.62f);
            var peakAccent = LerpColor(accentColor, Color.White, 0.62f);

            return new PlanetTheme(
                key,
                displayName,
                supportsLife: false,
                terrainBands:
                [
                    new TerrainColorBand(0f, 0.14f, deepPrimary, lowPrimary),
                    new TerrainColorBand(0.14f, 0.28f, lowPrimary, lowAccent),
                    new TerrainColorBand(0.28f, 0.44f, lowAccent, midPrimary),
                    new TerrainColorBand(0.44f, 0.6f, midPrimary, midAccent),
                    new TerrainColorBand(0.6f, 0.76f, midAccent, highPrimary),
                    new TerrainColorBand(0.76f, 0.9f, highPrimary, highAccent),
                    new TerrainColorBand(0.9f, 1f, peakPrimary, peakAccent)
                ],
                riverLowColor: Color.Transparent,
                riverHighColor: Color.Transparent,
                waterLowColor: Color.Transparent,
                waterHighColor: Color.Transparent);
        }

        private static int CombineThemeSeed(string key, string displayName, Color primary, Color accent)
        {
            var hash = new HashCode();
            hash.Add(key, StringComparer.Ordinal);
            hash.Add(displayName, StringComparer.Ordinal);
            hash.Add(primary.PackedValue);
            hash.Add(accent.PackedValue);
            return hash.ToHashCode();
        }

        private static float GetNormalizedSeed(int seed, int salt)
        {
            unchecked
            {
                var value = seed ^ (salt * 0x9e3779b9);
                value ^= value >> 16;
                value *= unchecked((int)0x7feb352d);
                value ^= value >> 15;
                value *= unchecked((int)0x846ca68b);
                value ^= value >> 16;
                return (uint)value / (float)uint.MaxValue;
            }
        }

        private static float WrapHue(float hue)
        {
            hue %= 1f;
            return hue < 0f ? hue + 1f : hue;
        }

        private static Color FromHsv(float hue, float saturation, float value)
        {
            hue = WrapHue(hue);
            saturation = MathHelper.Clamp(saturation, 0f, 1f);
            value = MathHelper.Clamp(value, 0f, 1f);

            if (saturation <= 0f)
            {
                var channel = (byte)Math.Clamp(value * 255f, 0f, 255f);
                return new Color(channel, channel, channel);
            }

            var scaledHue = hue * 6f;
            var sector = (int)MathF.Floor(scaledHue) % 6;
            var fraction = scaledHue - MathF.Floor(scaledHue);
            var p = value * (1f - saturation);
            var q = value * (1f - (saturation * fraction));
            var t = value * (1f - (saturation * (1f - fraction)));

            return sector switch
            {
                0 => ToColor(value, t, p),
                1 => ToColor(q, value, p),
                2 => ToColor(p, value, t),
                3 => ToColor(p, q, value),
                4 => ToColor(t, p, value),
                _ => ToColor(value, p, q)
            };
        }

        private static Color ToColor(float red, float green, float blue)
        {
            return new Color(
                (byte)Math.Clamp(red * 255f, 0f, 255f),
                (byte)Math.Clamp(green * 255f, 0f, 255f),
                (byte)Math.Clamp(blue * 255f, 0f, 255f));
        }

        private static Color LerpColor(Color start, Color end, float amount)
        {
            return Color.Lerp(start, end, MathHelper.Clamp(amount, 0f, 1f));
        }

        public readonly record struct TerrainColorBand(float StartHeight, float EndHeight, Color StartColor, Color EndColor);
    }
}
