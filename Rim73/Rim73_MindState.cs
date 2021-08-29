using System;
using HarmonyLib;
using System.Reflection;
using Verse;
using UnityEngine;
using RimWorld;
using System.Runtime.CompilerServices;
using Verse.AI;
using System.Collections.Generic;

namespace Rim73
{
    class Rim73_MindState
    {
        public static Dictionary<int, NearbyEnemiesCache> NearbyEnemiesDataCache;

        // Using struct to improve performance by using values instead of references
        // Coupled with per game dictionary fixed size, this improves the perf
        public struct NearbyEnemiesCache
        {
            public int lastTick;
            public bool enemiesNeaby;
        }

        public static void InitCache()
        {
            // Caching every faction inside a dictionary, this will be game wide instead of map-wide
            // TODO :: implement this Map wide instead of game wide
            int factionAmount = Find.FactionManager.AllFactions.EnumerableCount();
            NearbyEnemiesDataCache = new Dictionary<int, NearbyEnemiesCache>(factionAmount);

            foreach (Faction faction in Find.FactionManager.AllFactions)
            {
                NearbyEnemiesCache enemiesCache;
                enemiesCache.lastTick = 0; // This 
                enemiesCache.enemiesNeaby = false;
                NearbyEnemiesDataCache[faction.loadID] = enemiesCache;
            }
        }


        [HarmonyPatch(typeof(RimWorld.GenLocalDate), "DayTick", new Type[] { typeof(Thing) })]
        static class MindStateTick
        {
            static bool Prefix(ref int __result)
            {
                if (Rim73_Settings.mindstate)
                {
                    // For some reason there's some intensive Pawn Earth Location to tick for days
                    // Let's simplify it by saying that if Ticks % 60000 == 0 then it's a new day...
                    // This function is only called here : Pawn_MindState
                    // if (GenLocalDate.DayTick((Thing) this.pawn) != 0)
                    // This takes about 20% of processing time, and frankly is overkill for just counting the amount of interactions per day
                    //__result = Rim73.Ticks % 60000;
                    // Further simplification, every 65,000 ticks we reset the interactions
                    // Players doesn't see a difference but he will feel the ticking
                    __result = (Rim73.Ticks & (65536 - 1));
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(PawnUtility), "EnemiesAreNearby", new Type[] { typeof(Pawn), typeof(int), typeof(bool) })]
        static class EnemiesNearbyTick
        {
            static bool Prefix(ref Pawn pawn, ref int regionsToScan, ref bool passDoors, ref bool __result)
            {
                // Skip if Mindstate is disabled
                if (!Rim73_Settings.mindstate)
                    return true;

                // This is animals, they don't need this unless they are in manhunter mode, so we skip
                if(pawn.Faction == null)
                {
                    // If they have a target in mind, then they have enemies nearby
                    __result = pawn.mindState.mentalStateHandler.CurState != null;
                    return false;
                }

                // For the rest of the pawns... cached by faction Id
                NearbyEnemiesCache enemiesCache = NearbyEnemiesDataCache[pawn.Faction.loadID];

                // It's been 1000 ticks, let's skip
                if (Rim73.Ticks > enemiesCache.lastTick)
                {
                    return true;
                }
                    

                __result = enemiesCache.enemiesNeaby;
                regionsToScan = 256; // Returning false still ticks it
                return false;
            }

            static void Postfix(ref Pawn pawn, ref int regionsToScan, ref bool passDoors, ref bool __result)
            {
                // Caching result
                if (pawn.Faction != null && regionsToScan != 256)
                {
                    NearbyEnemiesCache enemiesCache = NearbyEnemiesDataCache[pawn.Faction.loadID];
                    enemiesCache.lastTick = Rim73.Ticks + 1000;
                    enemiesCache.enemiesNeaby = __result;
                    NearbyEnemiesDataCache[pawn.Faction.loadID] = enemiesCache;
                }
            }
        }

    }
}
