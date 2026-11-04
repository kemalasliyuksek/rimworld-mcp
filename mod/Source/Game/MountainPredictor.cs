using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using RimWorldMcp.Bridge.Core;
using Verse;
using Verse.Noise;

namespace RimWorldMcp.Bridge.Game
{
    /// <summary>
    /// Predicts where rock will be on a tile without generating a map. The game derives a map's
    /// noise from the world seed and the tile, so running the same steps with the same random
    /// sequence gives the same mountains. Must stay in step with GenStep_ElevationFertility and
    /// TileMutatorWorker_Mountain, including the order of every random call.
    /// </summary>
    internal static class MountainPredictor
    {
        private const string SourceType = nameof(GenStep_ElevationFertility);

        private static readonly FloatRange WarpFreqRange = ReadPrivate<FloatRange>("WarpFreqRange");
        private static readonly FloatRange WarpStrengthRange = ReadPrivate<FloatRange>("WarpStrengthRange");
        private static readonly IntRange WarpOctavesRange = ReadPrivate<IntRange>("WarpOctavesRange");
        private static readonly FloatRange PreTurbulenceStretchRange = ReadPrivate<FloatRange>("PreTurbulenceStretchRange");
        private static readonly FloatRange PostTurbulenceStretchRange = ReadPrivate<FloatRange>("PostTurbulenceStretchRange");

        private static readonly int ElevationSeedPart = new GenStep_ElevationFertility().SeedPart;
        private static readonly int MutatorSeedPart = new GenStep_MutatorPostElevationFertility().SeedPart;

        // Reading these keeps the prediction right if a patch changes the values. A missing field
        // means the game changed, and should fail loudly rather than predict the wrong mountains.
        private static T ReadPrivate<T>(string name)
        {
            FieldInfo field = typeof(GenStep_ElevationFertility).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException($"{SourceType}.{name} no longer exists. The mountain prediction needs updating for this version of the game.");
            }
            return (T)field.GetValue(null);
        }

        public sealed class Prediction
        {
            /// <summary>Indexed as [x, z] in samples.</summary>
            public bool[,] Rock;

            /// <summary>Tile features that also shape the terrain but are not modeled here.</summary>
            public List<string> Unmodeled;

            public bool IsExact => Unmodeled.Count == 0;
        }

        public static Prediction Predict(SurfaceTile tile, int mapSize, int cellsPerSample)
        {
            PlanetTile planetTile = tile.tile;
            int seed = Gen.HashCombineInt(Find.World.info.Seed, planetTile.GetHashCode());

            IList<TileMutatorDef> mutators = tile.Mutators;
            var unmodeled = mutators
                .Where(m => !(m.Worker is TileMutatorWorker_Mountain))
                .Select(m => m.defName)
                .ToList();

            ModuleBase elevation = BuildElevation(tile, seed);
            ModuleBase mountain = null;
            if (mutators.Any(m => m.Worker is TileMutatorWorker_Mountain))
            {
                // Mutators share one random sequence, so this is only exact if it runs first.
                mountain = BuildMountain(planetTile, seed, mapSize);
                if (!(mutators[0].Worker is TileMutatorWorker_Mountain))
                {
                    unmodeled.Add("(mutator order)");
                }
            }

            int samples = mapSize / cellsPerSample;
            var rock = new bool[samples, samples];
            for (int x = 0; x < samples; x++)
            {
                for (int z = 0; z < samples; z++)
                {
                    var cell = new IntVec3(x * cellsPerSample, 0, z * cellsPerSample);
                    float value = elevation.GetValue(cell);
                    if (mountain != null)
                    {
                        value += mountain.GetValue(cell);
                    }
                    rock[x, z] = value > MapGenTuning.ThresholdElevationRock;
                }
            }

            return new Prediction { Rock = rock, Unmodeled = unmodeled };
        }

        private static ModuleBase BuildElevation(SurfaceTile tile, int seed)
        {
            Rand.PushState(Gen.HashCombineInt(seed, ElevationSeedPart));
            try
            {
                ModuleBase module = new Perlin(GenStep_ElevationFertility.ElevationFreqRange.RandomInRange, 2.0, 0.5, 3, Rand.Range(0, int.MaxValue), QualityMode.High);
                module = MapNoiseUtility.AddDisplacementNoise(module, GenStep_ElevationFertility.DetailFreqRange.RandomInRange, GenStep_ElevationFertility.DetailStrengthRange.RandomInRange, GenStep_ElevationFertility.DetailOctavesRange.RandomInRange);
                module = new Scale(PreTurbulenceStretchRange.RandomInRange, 1.0, 1.0, module);
                module = new Rotate(0.0, Rand.Range(0f, 180f), 0.0, module);
                module = MapNoiseUtility.AddDisplacementNoise(module, WarpFreqRange.RandomInRange, WarpStrengthRange.RandomInRange, WarpOctavesRange.RandomInRange);
                module = new Scale(PostTurbulenceStretchRange.RandomInRange, 1.0, 1.0, module);
                module = new Rotate(0.0, Rand.Range(0f, 180f), 0.0, module);
                module = new ScaleBias(0.5, 0.5, module);
                return new Multiply(module, new Const(ElevationFactor(tile.HillinessForElevationGen)));
            }
            finally
            {
                Rand.PopState();
            }
        }

        private static float ElevationFactor(Hilliness hilliness)
        {
            switch (hilliness)
            {
                case Hilliness.Flat:
                    return MapGenTuning.ElevationFactorFlat;
                case Hilliness.SmallHills:
                    return MapGenTuning.ElevationFactorSmallHills;
                case Hilliness.LargeHills:
                    return MapGenTuning.ElevationFactorLargeHills;
                case Hilliness.Mountainous:
                    return MapGenTuning.ElevationFactorMountains;
                case Hilliness.Impassable:
                    return MapGenTuning.ElevationFactorImpassableMountains;
                default:
                    return 1f;
            }
        }

        private static ModuleBase BuildMountain(PlanetTile tile, int seed, int mapSize)
        {
            Rand.PushState(Gen.HashCombineInt(seed, MutatorSeedPart));
            try
            {
                float angle = (Find.World.CoastAngleAt(tile, BiomeDefOf.Ocean) ?? Find.World.CoastAngleAt(tile, BiomeDefOf.Lake)) ?? (float)Rand.Range(0, 360);
                ModuleBase module = new DistFromAxis_Directional(mapSize * 0.15f);
                module = new Translate(-mapSize * 0.2f, 0.0, 0.0, module);
                module = new ScaleBias(0.5, 0.5, module);
                module = new Clamp(0.0, 1.0, module);
                module = new Rotate(0.0, angle, 0.0, module);
                module = new Translate(-mapSize / 2f, 0.0, -mapSize / 2f, module);
                return MapNoiseUtility.AddDisplacementNoise(module, 0.003f, 20f, 2);
            }
            finally
            {
                Rand.PopState();
            }
        }
    }
}
