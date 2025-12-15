using Verse;
using System.Collections.Generic;

namespace MurderRimCore
{
    public class RandomSkinColorsExtension : DefModExtension
    {
        public List<ColorInt> skinColors;
        public bool AllowMixing = false; // If true, allows color mixing
        public float Saturation = 0f;    // 0 = default, 1 = white, can be any value between 0 and 1
        public float HairColorMatchChance = 0f; // 0 = never, 1 = always, 0.5 = 50%
    }
}

