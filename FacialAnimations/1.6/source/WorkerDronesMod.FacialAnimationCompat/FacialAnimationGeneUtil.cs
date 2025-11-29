using System;
using System.Collections.Concurrent;
using System.Reflection;
using Verse;

namespace MurderRimCore.FacialAnimationCompat
{
    internal class ReflectionCache
    {
        public readonly FieldInfo PawnField;
        public readonly PropertyInfo FaceTypeProp;
        public readonly MethodInfo ReloadIfNeed;

        public ReflectionCache(Type type)
        {
            PawnField = type.GetField("pawn", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            FaceTypeProp = type.GetProperty("FaceType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            ReloadIfNeed = type.GetMethod("ReloadIfNeed", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
    }

    public static class FacialAnimationGeneUtil
    {
        // Use a thread-safe per-Type cache to avoid cross-type FieldInfo misuse
        private static readonly ConcurrentDictionary<Type, ReflectionCache> cache = new ConcurrentDictionary<Type, ReflectionCache>();

        public static void SafeReload(object comp)
        {
            if (comp == null) return;

            var compType = comp.GetType();

            // Get or create the reflection cache for this exact component type
            var rc = cache.GetOrAdd(compType, t => new ReflectionCache(t));

            // If there is no reload method, nothing to do
            if (rc.ReloadIfNeed == null) return;

            Pawn pawnVal = null;
            try
            {
                if (rc.PawnField != null)
                    pawnVal = rc.PawnField.GetValue(comp) as Pawn;
            }
            catch (ArgumentException ae)
            {
                // FieldInfo was not valid for this instance for some reason; log and bail
                Log.Error($"[MurderRimCore] FacialAnimation SafeReload: ArgumentException reading pawn field for comp type {compType.FullName}: {ae}");
                return;
            }
            catch (Exception ex)
            {
                Log.Error($"[MurderRimCore] FacialAnimation SafeReload: Unexpected exception reading pawn field for comp type {compType.FullName}: {ex}");
                return;
            }

            if (pawnVal == null || pawnVal.DestroyedOrNull()) return;

            try
            {
                // If FaceType property exists and is null, don't reload
                if (rc.FaceTypeProp != null && rc.FaceTypeProp.GetValue(comp) == null) return;

                // Call ReloadIfNeed
                rc.ReloadIfNeed.Invoke(comp, null);
            }
            catch (TargetInvocationException tie)
            {
                Log.Error($"[MurderRimCore] FacialAnimation SafeReload: error invoking ReloadIfNeed on {compType.FullName}: {tie.InnerException ?? tie}");
            }
            catch (Exception ex)
            {
                Log.Error($"[MurderRimCore] FacialAnimation SafeReload: unexpected error while invoking ReloadIfNeed on {compType.FullName}: {ex}");
            }
        }
    }
}