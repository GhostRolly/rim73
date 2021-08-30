using System;
using System.Collections.Generic;
using HarmonyLib;
using System.Reflection;
using Verse;
using UnityEngine;
using RimWorld;
using System.Runtime.CompilerServices;

namespace Rim73
{
    class Rim73_Misc
    {
        // This patching is done because Rocketman does it aswell and I can't be bothered to do the checks to see if Rocketman is loaded
        // this also adds overhead in my code, which I don't want to do.
        // Calling IsHashIntervalTick() in Vanilla is a mess, takes too long to process!
        // So I'm doing it the easy way, thingIdNumber + ticks
        
        [HarmonyPatch(typeof(Gen), "IsHashIntervalTick", new Type[] { typeof(Thing), typeof(int) })]
        static class GenPatch
        {
            static bool Prefix(ref Thing t, ref int interval, ref bool __result)
            {
                /*
                // Unfortunately, requires too much work...
                interval |= interval >> 1;

                // This makes sure 4 bits
                // (From MSB and including MSB)
                // are set. It does following
                // 110011001 | 001100110 = 111111111
                interval |= interval >> 2;

                interval |= interval >> 4;
                interval |= interval >> 8;
                interval |= interval >> 16;

                // Increment n by 1 so that
                // there is only one set bit
                // which is just before original
                // MSB. n now becomes 1000000000
                interval = interval + 1;

                // Return original MSB after shifting.
                // n now becomes 100000000
                __result = ((Rim73.Ticks + t.thingIDNumber) & interval) == 0;

                */

                __result = (Rim73.Ticks + t.thingIDNumber) % interval == 0;
                //__result = (Rim73.Ticks + t.thingIDNumber) & ((interval | 3) - 1)) == 0;
                return false;
            }
        }

        [HarmonyPatch(typeof(Map), "MapPreTick", new Type[] { })]
        static class TickPatch
        {
            static bool Prefix()
            {
                Rim73.Ticks = Find.TickManager.TicksGame + 1;
                return true;
            }
        }
    }
}
