using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MurderRimCore
{
    // Manages persistent (attached) observation effecters.
    // Only needed if attachEffecter=true in ObservationLearningExtension.
    public class MurderRimCore_ObservationEffecterManager : GameComponent
    {
        private class Active
        {
            public Pawn pawn;
            public Effecter effecter;
            public EffecterDef def;
            public int expireTick;
        }

        private readonly Dictionary<int, Active> _active = new Dictionary<int, Active>(128);
        private readonly List<int> _removalBuffer = new List<int>(64);

        public MurderRimCore_ObservationEffecterManager() { }
        public MurderRimCore_ObservationEffecterManager(Game game) { }

        public static MurderRimCore_ObservationEffecterManager Instance
        {
            get
            {
                var game = Verse.Current.Game; // Current is static; Game may be null early
                return game != null ? game.GetComponent<MurderRimCore_ObservationEffecterManager>() : null;
            }
        }

        public override void GameComponentTick()
        {
            if (_active.Count == 0) return;

            int now = Find.TickManager.TicksGame;
            _removalBuffer.Clear();

            foreach (var kv in _active)
            {
                var a = kv.Value;
                if (a == null || a.pawn == null || a.effecter == null)
                {
                    Cleanup(kv.Key, a);
                    _removalBuffer.Add(kv.Key);
                    continue;
                }

                if (!a.pawn.Spawned || a.pawn.Map == null || a.pawn.Destroyed)
                {
                    Cleanup(kv.Key, a);
                    _removalBuffer.Add(kv.Key);
                    continue;
                }

                if (now >= a.expireTick)
                {
                    Cleanup(kv.Key, a);
                    _removalBuffer.Add(kv.Key);
                    continue;
                }

                TargetInfo ti = a.pawn;
                a.effecter.EffectTick(ti, ti);
            }

            for (int i = 0; i < _removalBuffer.Count; i++)
                _active.Remove(_removalBuffer[i]);
        }

        public void EnsureAttached(Pawn pawn, EffecterDef def, int durationTicks, bool extendDuration)
        {
            if (pawn == null || def == null) return;

            int key = pawn.thingIDNumber;
            int now = Find.TickManager.TicksGame;
            int dur = durationTicks <= 0 ? 60 : durationTicks;

            Active a;
            if (!_active.TryGetValue(key, out a) || a.effecter == null || a.def != def)
            {
                var eff = def.Spawn();
                a = new Active
                {
                    pawn = pawn,
                    effecter = eff,
                    def = def,
                    expireTick = now + dur
                };
                _active[key] = a;

                TargetInfo ti = pawn;
                eff.EffectTick(ti, ti); // prime first frame
                return;
            }

            int newExpiry = now + dur;
            if (extendDuration || a.expireTick < newExpiry)
                a.expireTick = newExpiry;
        }

        public void Remove(Pawn pawn)
        {
            if (pawn == null) return;
            int key = pawn.thingIDNumber;
            Active a;
            if (_active.TryGetValue(key, out a))
            {
                Cleanup(key, a);
                _active.Remove(key);
            }
        }

        private void Cleanup(int key, Active a)
        {
            try
            {
                if (a != null && a.effecter != null)
                    a.effecter.Cleanup();
            }
            catch
            {
                // ignore
            }
        }
    }
}