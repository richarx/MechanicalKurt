using System;

public static class TrafficSpawnLayout
{
    public static float GetRequiredSpacing(float firstLength, float firstGap,
        float secondLength, float secondGap, float extraGap)
    {
        return (firstLength + secondLength) * 0.5f + Math.Max(firstGap, secondGap) + extraGap;
    }

    public static bool TryCreateDistances(float routeLength, float[] lengths, float[] minimumGaps,
        float standardDeviation, float extraGap, Random random, out float[] distances)
    {
        distances = Array.Empty<float>();
        int count = lengths.Length;
        if (count == 0 || minimumGaps.Length != count || routeLength <= 0f)
            return false;

        float[] requiredGaps = new float[count];
        float requiredLength = 0f;
        for (int i = 0; i < count; i++)
        {
            int next = (i + 1) % count;
            requiredGaps[i] = GetRequiredSpacing(lengths[i], minimumGaps[i], lengths[next], minimumGaps[next], extraGap);
            requiredLength += requiredGaps[i];
        }
        if (requiredLength > routeLength)
            return false;

        float freeLength = routeLength - requiredLength;
        double[] weights = new double[count];
        double weightSum = 0d;
        for (int i = 0; i < count; i++)
        {
            double variation = 0d;
            if (standardDeviation > 0f)
            {
                // Box-Muller: a normal sample with mean 0 and standard deviation 1.
                double u = 1d - random.NextDouble();
                double v = random.NextDouble();
                variation = Math.Sqrt(-2d * Math.Log(u)) * Math.Cos(2d * Math.PI * v) * standardDeviation;
            }
            weights[i] = Math.Max(0d, freeLength / count + variation);
            weightSum += weights[i];
        }

        distances = new float[count];
        double distance = random.NextDouble() * routeLength;
        for (int i = 0; i < count; i++)
        {
            distances[i] = (float)(distance % routeLength);
            double freeGap = weightSum > 0d ? freeLength * weights[i] / weightSum : freeLength / count;
            distance += requiredGaps[i] + freeGap;
        }
        return true;
    }
}
