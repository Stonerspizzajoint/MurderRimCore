using System;
using System.Collections.Generic;
using UnityEngine;

namespace MurderRimCore
{
    public static class SkinColorUtils
    {
        private static readonly System.Random _random = new System.Random();

        public static Color GetRandomOrMixedColor(List<Color> colorList, bool allowMixing, float saturation = 0f)
        {
            if (colorList == null || colorList.Count == 0)
                return Color.white;

            Color result;
            if (allowMixing && colorList.Count > 1)
            {
                if (UnityEngine.Random.value < 0.5f)
                {
                    result = colorList[UnityEngine.Random.Range(0, colorList.Count)];
                }
                else
                {
                    Color c1 = colorList[UnityEngine.Random.Range(0, colorList.Count)];
                    Color c2 = colorList[UnityEngine.Random.Range(0, colorList.Count)];
                    if (colorList.Count > 2)
                    {
                        while (c2 == c1)
                            c2 = colorList[UnityEngine.Random.Range(0, colorList.Count)];
                    }
                    float t = UnityEngine.Random.Range(0f, 1f);
                    result = Color.Lerp(c1, c2, t);
                }
            }
            else
            {
                result = colorList[UnityEngine.Random.Range(0, colorList.Count)];
            }

            return ApplySaturation(result, saturation);
        }

        public static Color ApplySaturation(Color color, float saturation)
        {
            saturation = Mathf.Clamp01(saturation);
            if (saturation == 0f)
                return color;
            return Color.Lerp(color, Color.white, saturation);
        }

        public static Color Darken(Color color, float darkness)
        {
            darkness = Mathf.Clamp01(darkness);
            return Color.Lerp(color, Color.black, darkness);
        }

        public static Color? GetHairColorWithVariance(Color baseColor, float darknessVariance, float unchangedChance)
        {
            if (UnityEngine.Random.value < unchangedChance)
                return null; // Leave unchanged

            float darkness = darknessVariance > 0f ? UnityEngine.Random.Range(0f, darknessVariance) : 0f;
            return Darken(baseColor, darkness);
        }
    }
}

