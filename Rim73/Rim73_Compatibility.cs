using System;
using System.Collections.Generic;
using HarmonyLib;
using System.Reflection;
using Verse;
using UnityEngine;
using RimWorld;

namespace Rim73
{
    public class Rim73_Compatibility
    {
        public static Harmony harmony;

        public static void DoAllCompatiblities(Harmony current)
        {
            // Storing harmony for further use
            harmony = current;

            if (Comp_Smartspeed.DoCompatibility())
                Log.Message("<color=green>Rim73 Compatibility</color> > Added compatibility with SmartSpeed");

        }

        public class Comp_Smartspeed
        {

            public static void SmartSpeed_Postfix(ref TimeSpeed currTimeSpeed, ref float __result)
            {
                if (__result == 15f && Rim73_Settings.warpSpeed)
                    __result = 20f;
            }

            public static bool DoCompatibility()
            {
                // Smartspeed compatibility
                try
                {
                    Assembly SmartSpeed_Assembly = Assembly.Load("SmartSpeed, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
                    Type SmartSpeed_TickManager = SmartSpeed_Assembly.GetType("SmartSpeed.Detouring.TickManager");
                    MethodInfo SmartSpeed_TickRate = SmartSpeed_TickManager.GetMethod("TickRate", BindingFlags.Instance | BindingFlags.NonPublic);
                    harmony.Patch(SmartSpeed_TickRate, null, new HarmonyMethod(typeof(Comp_Smartspeed).GetMethod("SmartSpeed_Postfix", BindingFlags.Public | BindingFlags.Static)));
                }
                catch (Exception ex)
                {
                    //Log.Warning("Error : " + ex.Message);
                    return false;
                }

                return true;
            }
        }
    }
}
