using System;
using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    // Minimal dynamic-disabled command to grey out cleanly without touching protected fields.
    public class Command_Action_Fusion : Command_Action
    {
        public Func<bool> DisabledGetter;

        public override bool Disabled => DisabledGetter != null && DisabledGetter();
    }
}