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
        public static Dictionary<int, Dictionary<int, bool>> MapFactionEnemies = new Dictionary<int, Dictionary<int, bool>>();

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
                __result = false;
                return false;
            }
        }


    }
}
