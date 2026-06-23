using System.Collections.Generic;
using UnityEngine;

// https://gist.github.com/a3geek/8532817159b77c727040cf67c92af322

namespace Metater
{
    // The algorithm is from the "Fast Poisson Disk Sampling in Arbitrary Dimensions" paper by Robert Bridson.
    // https://www.cs.ubc.ca/~rbridson/docs/bridson-siggraph07-poissondisk.pdf

    public static class FastPoissonDiskSampling
    {
        public const float InvertRootTwo = 0.70710678118f; // Becaust two dimension grid.
        public const int DefaultIterationPerPoint = 30;

        #region "Structures"
        private class Settings
        {
            public Vector2 BottomLeft;
            public Vector2 TopRight;
            public Vector2 Center;
            public Rect Dimension;

            public float MinimumDistance;
            public int IterationPerPoint;

            public float CellSize;
            public int GridWidth;
            public int GridHeight;
        }

        private class Bags
        {
            public Vector2?[,] Grid;
            public List<Vector2> SamplePoints;
            public List<Vector2> ActivePoints;
        }
        #endregion


        public static List<Vector2> Sampling(Vector2 bottomLeft, Vector2 topRight, float minimumDistance)
        {
            return Sampling(bottomLeft, topRight, minimumDistance, DefaultIterationPerPoint);
        }

        public static List<Vector2> Sampling(Vector2 bottomLeft, Vector2 topRight, float minimumDistance, int iterationPerPoint)
        {
            var settings = GetSettings(
                bottomLeft,
                topRight,
                minimumDistance,
                iterationPerPoint <= 0 ? DefaultIterationPerPoint : iterationPerPoint
            );

            var bags = new Bags()
            {
                Grid = new Vector2?[settings.GridWidth + 1, settings.GridHeight + 1],
                SamplePoints = new List<Vector2>(),
                ActivePoints = new List<Vector2>()
            };

            GetFirstPoint(settings, bags);

            do
            {
                var index = Random.Range(0, bags.ActivePoints.Count);

                var point = bags.ActivePoints[index];

                var found = false;
                for (var k = 0; k < settings.IterationPerPoint; k++)
                {
                    found = found | GetNextPoint(point, settings, bags);
                }

                if (found == false)
                {
                    bags.ActivePoints.RemoveAt(index);
                }
            }
            while (bags.ActivePoints.Count > 0);

            return bags.SamplePoints;
        }

        #region "Algorithm Calculations"
        private static bool GetNextPoint(Vector2 point, Settings set, Bags bags)
        {
            var found = false;
            var p = GetRandPosInCircle(set.MinimumDistance, 2f * set.MinimumDistance) + point;

            if (set.Dimension.Contains(p) == false)
            {
                return false;
            }

            var minimum = set.MinimumDistance * set.MinimumDistance;
            var index = GetGridIndex(p, set);
            var drop = false;

            // Although it is Mathf.CeilToInt(set.MinimumDistance / set.CellSize) in the formula, It will be 2 after all.
            var around = 2;
            var fieldMin = new Vector2Int(Mathf.Max(0, index.x - around), Mathf.Max(0, index.y - around));
            var fieldMax = new Vector2Int(Mathf.Min(set.GridWidth, index.x + around), Mathf.Min(set.GridHeight, index.y + around));

            for (var i = fieldMin.x; i <= fieldMax.x && drop == false; i++)
            {
                for (var j = fieldMin.y; j <= fieldMax.y && drop == false; j++)
                {
                    var q = bags.Grid[i, j];
                    if (q.HasValue == true && (q.Value - p).sqrMagnitude <= minimum)
                    {
                        drop = true;
                    }
                }
            }

            if (drop == false)
            {
                found = true;

                bags.SamplePoints.Add(p);
                bags.ActivePoints.Add(p);
                bags.Grid[index.x, index.y] = p;
            }

            return found;
        }

        private static void GetFirstPoint(Settings set, Bags bags)
        {
            var first = new Vector2(
                Random.Range(set.BottomLeft.x, set.TopRight.x),
                Random.Range(set.BottomLeft.y, set.TopRight.y)
            );

            var index = GetGridIndex(first, set);

            bags.Grid[index.x, index.y] = first;
            bags.SamplePoints.Add(first);
            bags.ActivePoints.Add(first);
        }
        #endregion

        #region "Utils"
        private static Vector2Int GetGridIndex(Vector2 point, Settings set)
        {
            return new Vector2Int(
                Mathf.FloorToInt((point.x - set.BottomLeft.x) / set.CellSize),
                Mathf.FloorToInt((point.y - set.BottomLeft.y) / set.CellSize)
            );
        }

        private static Settings GetSettings(Vector2 bl, Vector2 tr, float min, int iteration)
        {
            var dimension = (tr - bl);
            var cell = min * InvertRootTwo;

            return new Settings()
            {
                BottomLeft = bl,
                TopRight = tr,
                Center = (bl + tr) * 0.5f,
                Dimension = new Rect(new Vector2(bl.x, bl.y), new Vector2(dimension.x, dimension.y)),

                MinimumDistance = min,
                IterationPerPoint = iteration,

                CellSize = cell,
                GridWidth = Mathf.CeilToInt(dimension.x / cell),
                GridHeight = Mathf.CeilToInt(dimension.y / cell)
            };
        }

        private static Vector2 GetRandPosInCircle(float fieldMin, float fieldMax)
        {
            var theta = Random.Range(0f, Mathf.PI * 2f);
            var radius = Mathf.Sqrt(Random.Range(fieldMin * fieldMin, fieldMax * fieldMax));

            return new Vector2(radius * Mathf.Cos(theta), radius * Mathf.Sin(theta));
        }
        #endregion
    }
}