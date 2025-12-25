using HarmonyLib;
using Verse;
using System.Reflection;

namespace MRHP.Patches
{
    public class MurderRimMod : Mod
    {
        public MurderRimMod(ModContentPack content) : base(content)
        {
            // This constructor runs BEFORE Defs are loaded.
            // Perfect for patching DefGenerators.

            var harmony = new Harmony("com.stonerspizzajoint.MRHP");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            Log.Message("[MRHP] Harmony Patches Initialized and Applied Early.");
        }
    }
}