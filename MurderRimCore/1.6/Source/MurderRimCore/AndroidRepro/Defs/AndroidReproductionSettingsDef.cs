using System.Linq;
using Verse;
using RimWorld;
using UnityEngine;

namespace MurderRimCore.AndroidRepro
{
    public class AndroidReproductionSettingsDef : Def
    {
        // --- Global Toggles ---
        public bool enabled = true;

        // --- UI & Visuals ---
        public string gizmoIconPath = "UI/Icons/Medical/GeneExtraction";
        public string uiTitle = "Neural Fusion Interface";

        // --- Selection Rules ---
        public bool requireAwakened = true;

        // REPLACED: public bool requireAdult = true; 
        public float minAge = 13f; // Default to 13 (Teen/Adult transition usually)

        public bool requireLover = true;
        public bool requireLoverOnMap = true;

        // --- Accessor Helper ---
        private static AndroidReproductionSettingsDef _cached;
        public static AndroidReproductionSettingsDef Current
        {
            get
            {
                if (_cached == null)
                {
                    _cached = DefDatabase<AndroidReproductionSettingsDef>.AllDefsListForReading.FirstOrDefault();
                }
                return _cached;
            }
        }

        public Texture2D Icon
        {
            get
            {
                if (!string.IsNullOrEmpty(gizmoIconPath))
                {
                    return ContentFinder<Texture2D>.Get(gizmoIconPath, true);
                }
                return null;
            }
        }
    }
}