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
        [HarmonyPatch(typeof(RimWorld.GenLocalDate), "DayTick", new Type[] { typeof(Thing) })]
        static class JobDriverTick
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
                    __result = Find.TickManager.TicksGame % 60000;
                    return false;
                }

                return true;
            }
        }
    }
}
