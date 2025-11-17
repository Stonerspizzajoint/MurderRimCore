using RimWorld;
using Verse;

namespace MurderRimCore.AndroidRepro
{
    public static class FusionSlotUtility
    {
        // Returns true only if both slots (A, B) were found.
        // A and B correspond to "left" and "right" relative to the station's facing (front) direction.
        // Preferred: the two cells directly in front of the footprint's front corners (matches your red cells),
        // centered on the interaction cell's axis.
        // Fallback: closest valid cells to the interaction cell, but kept on the "front" side and outside the footprint.
        public static bool TryFindFusionSlots(VREAndroids.Building_AndroidCreationStation station, out IntVec3 slotA, out IntVec3 slotB)
        {
            slotA = IntVec3.Invalid;
            slotB = IntVec3.Invalid;

            if (station?.Map == null) return false;

            Map map = station.Map;
            CellRect rect = GenAdj.OccupiedRect(station.Position, station.Rotation, station.def.size);
            IntVec3 ic = station.InteractionCell;

            // Determine which side is "front" by checking where the interaction cell sits relative to the footprint
            // Prefer robust detection from actual geometry rather than relying on Rotation assumptions.
            // front = side for which the interaction cell sits exactly 1 tile outside the rect along that side.
            FrontSide side = DetectFrontSide(rect, ic);

            // Compute the two ideal (preferred) outside cells (just beyond the front edge corners).
            IntVec3 prefLeft, prefRight;
            GetPreferredFrontCornerOutsideCells(rect, side, out prefLeft, out prefRight);

            // Validate preferred slots
            bool leftOk = IsValidFrontSlot(prefLeft, map, rect, side, ic);
            bool rightOk = IsValidFrontSlot(prefRight, map, rect, side, ic);

            if (leftOk && rightOk)
            {
                slotA = prefLeft;
                slotB = prefRight;
                return true;
            }

            // If either preferred slot is blocked, find the closest alternative(s) to the interaction cell on the front side.
            // We scan radially from the interaction cell, keeping only "front-side-only" candidates and excluding the center/footprint.
            IntVec3 alt1 = IntVec3.Invalid;
            IntVec3 alt2 = IntVec3.Invalid;

            // Helper to accept a candidate only if it obeys all constraints and isn't already chosen
            bool AcceptCandidate(IntVec3 c, IntVec3 already)
            {
                if (!IsValidFrontSlot(c, map, rect, side, ic)) return false;
                if (already.IsValid && c == already) return false;
                return true;
            }

            // Keep any still-valid preferred as first choices, then fill the other(s) from the scan
            if (leftOk) alt1 = prefLeft;
            if (rightOk)
            {
                if (!alt1.IsValid) alt1 = prefRight;
                else alt2 = prefRight;
            }

            // If we still need one or two slots, search outward from the interaction cell
            if (!alt1.IsValid || !alt2.IsValid)
            {
                // Reasonable search radius; keep inexpensive since this can run during float-menu open too.
                // We search up to radius 6 to tolerate messy fronts; tune as needed.
                const int MaxRadius = 6;
                foreach (var c in GenRadial.RadialCellsAround(ic, MaxRadius, useCenter: false))
                {
                    if (!c.InBounds(map)) continue;
                    if (rect.Contains(c)) continue; // never inside the footprint
                    if (c == ic) continue;
                    if (!IsOnFrontSide(c, rect, side)) continue; // keep on front side only

                    if (!alt1.IsValid)
                    {
                        if (AcceptCandidate(c, already: IntVec3.Invalid))
                        {
                            alt1 = c;
                            // if we already also have alt2, break early
                            if (alt2.IsValid) break;
                            continue;
                        }
                    }
                    else if (!alt2.IsValid)
                    {
                        if (AcceptCandidate(c, already: alt1))
                        {
                            alt2 = c;
                            break;
                        }
                    }
                }
            }

            // If we have only one from scanning and one preferred was valid, combine them
            if (!alt1.IsValid && leftOk) alt1 = prefLeft;
            if (!alt1.IsValid && rightOk) alt1 = prefRight;
            if (!alt2.IsValid && leftOk && prefLeft != alt1) alt2 = prefLeft;
            if (!alt2.IsValid && rightOk && prefRight != alt1) alt2 = prefRight;

            if (alt1.IsValid && alt2.IsValid)
            {
                // Order the pair as "left/right" relative to the station's facing side for determinism.
                OrderAsLeftRight(side, rect, ref alt1, ref alt2);
                slotA = alt1;
                slotB = alt2;
                return true;
            }

            return false;
        }

        // Identify which side of the rect the interaction cell is on.
        private enum FrontSide { North, East, South, West }

        private static FrontSide DetectFrontSide(CellRect rect, IntVec3 ic)
        {
            // Check if ic is one cell beyond each edge and aligned within the span of that edge.
            if (ic.z == rect.maxZ + 1 && ic.x >= rect.minX && ic.x <= rect.maxX) return FrontSide.North;
            if (ic.z == rect.minZ - 1 && ic.x >= rect.minX && ic.x <= rect.maxX) return FrontSide.South;
            if (ic.x == rect.maxX + 1 && ic.z >= rect.minZ && ic.z <= rect.maxZ) return FrontSide.East;
            if (ic.x == rect.minX - 1 && ic.z >= rect.minZ && ic.z <= rect.maxZ) return FrontSide.West;

            // Fallback: infer from which edge ic is closest to (handles odd defs). Use simplest distance heuristic.
            int dNorth = ic.z - rect.maxZ;
            int dSouth = rect.minZ - ic.z;
            int dEast = ic.x - rect.maxX;
            int dWest = rect.minX - ic.x;

            int best = int.MinValue;
            FrontSide side = FrontSide.North;

            void Consider(int val, FrontSide s)
            {
                if (val > best)
                {
                    best = val;
                    side = s;
                }
            }

            Consider(dNorth, FrontSide.North);
            Consider(dSouth, FrontSide.South);
            Consider(dEast, FrontSide.East);
            Consider(dWest, FrontSide.West);

            return side;
        }

        // Preferred outside cells: the two cells directly in front of the front-edge corners.
        // These correspond to your red cells, with the interaction cell centered between them.
        private static void GetPreferredFrontCornerOutsideCells(CellRect rect, FrontSide side, out IntVec3 left, out IntVec3 right)
        {
            switch (side)
            {
                case FrontSide.North:
                    // Front edge row at z = rect.maxZ; outside is +z
                    left = new IntVec3(rect.minX, 0, rect.maxZ + 1);
                    right = new IntVec3(rect.maxX, 0, rect.maxZ + 1);
                    break;

                case FrontSide.South:
                    // Front edge row at z = rect.minZ; outside is -z
                    // Left/right swap because facing south flips x-handedness
                    left = new IntVec3(rect.maxX, 0, rect.minZ - 1);
                    right = new IntVec3(rect.minX, 0, rect.minZ - 1);
                    break;

                case FrontSide.East:
                    // Front edge column at x = rect.maxX; outside is +x
                    // Facing east: left is +z, so use top corner as left
                    left = new IntVec3(rect.maxX + 1, 0, rect.maxZ);
                    right = new IntVec3(rect.maxX + 1, 0, rect.minZ);
                    break;

                case FrontSide.West:
                    // Front edge column at x = rect.minX; outside is -x
                    // Facing west: left is -z (south), so use bottom corner as left
                    left = new IntVec3(rect.minX - 1, 0, rect.minZ);
                    right = new IntVec3(rect.minX - 1, 0, rect.maxZ);
                    break;

                default:
                    left = IntVec3.Invalid;
                    right = IntVec3.Invalid;
                    break;
            }
        }

        // Keep candidates in front, standable, not in footprint, not the interaction cell.
        private static bool IsValidFrontSlot(IntVec3 c, Map map, CellRect footprint, FrontSide side, IntVec3 ic)
        {
            if (!c.InBounds(map)) return false;
            if (footprint.Contains(c)) return false;
            if (c == ic) return false;
            if (!IsOnFrontSide(c, footprint, side)) return false;
            if (!c.Standable(map)) return false;       // walkable + not occupied by impassable edifice
            if (c.Filled(map)) return false;           // avoid things that fill the cell (e.g., buildings)
            return true;
        }

        private static bool IsOnFrontSide(IntVec3 c, CellRect rect, FrontSide side)
        {
            switch (side)
            {
                case FrontSide.North: return c.z >= rect.maxZ + 1;
                case FrontSide.South: return c.z <= rect.minZ - 1;
                case FrontSide.East: return c.x >= rect.maxX + 1;
                case FrontSide.West: return c.x <= rect.minX - 1;
                default: return false;
            }
        }

        // Ensure returned pair preserves "left/right" semantics for consistency.
        private static void OrderAsLeftRight(FrontSide side, CellRect rect, ref IntVec3 a, ref IntVec3 b)
        {
            bool swap = false;
            switch (side)
            {
                case FrontSide.North:
                    // Left = lower X at same z-row
                    if (a.x > b.x) swap = true;
                    break;
                case FrontSide.South:
                    // Facing south flips handedness: left = higher X
                    if (a.x < b.x) swap = true;
                    break;
                case FrontSide.East:
                    // Facing east: left = higher Z
                    if (a.z < b.z) swap = true;
                    break;
                case FrontSide.West:
                    // Facing west: left = lower Z
                    if (a.z > b.z) swap = true;
                    break;
            }
            if (swap)
            {
                var tmp = a;
                a = b;
                b = tmp;
            }
        }
    }
}