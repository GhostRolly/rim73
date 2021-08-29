using System;
using HarmonyLib;
using System.Reflection;
using Verse;
using UnityEngine;
using RimWorld;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace Rim73
{
    class Rim73_Pather
    {
        // Region Caching
        public static Dictionary<int, int?> RegionCache = new Dictionary<int, int?>(2048);

        public static void InitRegionCache()
        {
            RegionCache.Clear();
        }

        // Inlined
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static List<Pawn> GetPawnsFromListThings(List<Thing> listThings, bool fast = false)
        {
            if (listThings.Count == 0)
                return new List<Pawn>();

            List<Pawn> listPawn = new List<Pawn>();

            for (int i = 0; listThings.Count > i; i++)
            {
                if (listThings[i] is Pawn)
                {
                    Pawn curPawn = listThings[i] as Pawn;
                    if (!curPawn.Dead && !curPawn.Downed)
                        listPawn.Add((Pawn)listThings[i]);

                    if (fast)
                        return listPawn;
                }
            }

            return listPawn;
        }

        // Pather Ticks
        [HarmonyPatch(typeof(Verse.AI.Pawn_PathFollower), "PatherTick", new Type[] { })]
        static class Pawn_PatherTick
        {
            static bool Prefix(ref Verse.AI.Pawn_PathFollower __instance, ref Pawn ___pawn, ref int ___foundPathWhichCollidesWithPawns, ref int ___lastMovedTick)
            {
                // Skip if disabled
                if (!Rim73_Settings.pather)
                    return true;

                // skip the dead and downed
                if (___pawn.health.State != PawnHealthState.Mobile)
                    return false;
                
                if (___pawn.mindState.anyCloseHostilesRecently && ___pawn.Faction != Faction.OfPlayer)
                {
                    //int currTicks = Find.TickManager.TicksGame;
                    int currTicks = Rim73.Ticks;
                    
                    // Sleeping for 80 ticks, fail-safe for Pawns who are moving and/or are fleeing
                    if (___foundPathWhichCollidesWithPawns + 60 > currTicks && !__instance.MovedRecently(40) && ___pawn.mindState.mentalStateHandler.CurStateDef != MentalStateDefOf.PanicFlee)
                        return false;

                    // If it has been more than 200 ticks, we tick vanilla once every 10 ticks
                    if (currTicks - ___lastMovedTick > 200 && currTicks % 10 == 0)
                        return true;

                    // If we haven't moved in more than 200 ticks and Vanilla didn't move us, then we don't even bother, we skip
                    if (currTicks - ___lastMovedTick > 200)
                        return false;

                    // If there's atleast a pawn in front of us, we update the Collision timer
                    if (GetPawnsFromListThings(___pawn.pather.nextCell.GetThingList(___pawn.Map), true).Count > 0)
                    {
                        ___foundPathWhichCollidesWithPawns = currTicks;
                    }
                    else
                    {
                        return true;
                    }
                }

                return true;

            }
        }


        [HarmonyPatch(typeof(RegionListersUpdater), "DeregisterInRegions", new Type[] { typeof(Thing), typeof(Map) })]
        static class RegionDeregesiterPatch
        {
            static bool Prefix(ref Thing thing, ref Map map)
            {
                // Skip if disabled
                if (!Rim73_Settings.pather)
                    return true;

                if (thing.Faction == null && !(thing is Pawn))
                    return true;

                int thingId = thing.thingIDNumber;
                int? curRegion = map.regionGrid.GetValidRegionAt_NoRebuild(thing.Position)?.id;

                if (RegionCache.ContainsKey(thingId))
                {
                    // Skip if still in same region
                    bool sameRegion = RegionCache[thingId] == curRegion;

                    if (!sameRegion)
                        RegionCache[thingId] = curRegion;
                    
                    return !sameRegion;
                }
                else
                {
                    RegionCache.SetOrAdd(thingId, curRegion);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(RegionListersUpdater), "RegisterInRegions", new Type[] { typeof(Thing), typeof(Map) })]
        static class RegionRegesiterPatch
        {
            static bool Prefix(ref Thing thing, ref Map map)
            {
                // Skip if disabled
                if (!Rim73_Settings.pather)
                    return true;

                if (thing.Faction == null && !(thing is Pawn))
                    return true;

                int thingId = thing.thingIDNumber;
                int? curRegion = map.regionGrid.GetValidRegionAt_NoRebuild(thing.Position)?.id;

                if ((Rim73.Ticks & (65535 - 1)) == 0)
                {
                    RegionCache.Clear();
                    return true;
                }

                if (RegionCache.ContainsKey(thingId))
                {
                    // Skip if still in same region
                    return RegionCache[thingId] == curRegion;
                } else
                {
                    return true;
                }

            }
        }

    }
}
