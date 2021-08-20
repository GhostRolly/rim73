using System;
using HarmonyLib;
using System.Reflection;
using Verse;
using UnityEngine;
using RimWorld;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Linq;

namespace Rim73
{
    class Rim73_Hediff
    {
        /* This will be used to BitMask and detect every and any hediff, we will use custom ticking to know what to do for each one of them */
        public static Dictionary<string, int> HediffComps_BitMaskDB = new Dictionary<string, int>(16);

        // HediffCaching
        // There is 93 Hediffs in vanilla, space for 931 more
        // This is use so that the dictionnary takes up 512  4 bytes memory cells, increases performance and prevents memory fragmentation
        // Downsides are : will crash if more than 512 hediffs detected and takes up 2048 kBytes in memory
        public static Dictionary<string, int> Hediff_BitMask = new Dictionary<string, int>(512);

        // Private Field Accessors
        public static FieldInfo HealPermanentWound_ticksToHeal;
        public static FieldInfo MessageAfterTicks_ticksUntilMessage;
        public static FieldInfo Infecter_ticksUntilInfect;
        public static FieldInfo KillAfterDays_ticksLeft;
        public static FieldInfo ImmunityHandler_immunityList;

        // Used for fast-access on private members (thanks Tynan)
        public static void InitFieldInfos()
        {
            HealPermanentWound_ticksToHeal = typeof(HediffComp_HealPermanentWounds).GetField("ticksToHeal", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            MessageAfterTicks_ticksUntilMessage = typeof(HediffComp_MessageAfterTicks).GetField("ticksUntilMessage", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            Infecter_ticksUntilInfect = typeof(HediffComp_Infecter).GetField("ticksUntilInfect", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            KillAfterDays_ticksLeft = typeof(HediffComp_KillAfterDays).GetField("ticksLeft", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            ImmunityHandler_immunityList = typeof(ImmunityHandler).GetField("immunityList", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
        }

        public enum HediffCompProps
        {
            HediffCompProperties_None =                     0x0,
            HediffCompProperties_Disappears =               0x01, // 1
            HediffCompProperties_TendDuration =             0x02, // 2
            HediffCompProperties_Immunizable =              0x04, // 4
            HediffCompProperties_Discoverable =             0x08, // 8
            /* HediffCompProperties_Effecter =              0x10, // This is ticked every time anyways by the DrawTrackerTick in Pawn_DrawTracker... Skipping */
            HediffCompProperties_SeverityPerDay =           0x10,
            /* HediffCompProperties_DrugEffectFactor =      0x0, // This doesn't get ticked, only called by Chemical */
            HediffCompProperties_HealPermanentWounds =      0x20,
            /* HediffCompProperties_VerbGiver =             0x0, // This is never ticked by the heddifs, instead it is used by Armor Penetration and other calculations... Skipping */
            /* HediffCompProperties_DissolveGearOnDeath =   0x0, // This is never ticked aswell, it uses Notify_PawnDied instead... Skipping */
            /* HediffCompProperties_RecoveryThought =       0x0, // This is never ticked, instead called on removal of Hediff CompPostPostRemoved()... Skipping */
            HediffCompProperties_MessageAfterTicks =        0x40,
            HediffCompProperties_GrowthMode =               0x80,
            /* HediffCompProperties_GetsPermanent =         0x0, // This is never ticked aswell, instead called by PreFinalizeInjury by the DamageWorker... Skipping */
            HediffCompProperties_Infecter =                 0x100,
            HediffCompProperties_KillAfterDays =            0x200,
            HediffCompProperties_CauseMentalState =         0x400,
            HediffCompProperties_SelfHeal =                 0x800
        };

        public static void PopulateHediffEnumDB()
        {
            // Seting up the DB
            string[] propsName = (string[])Enum.GetNames(typeof(HediffCompProps));
            HediffCompProps[] propsVals = (HediffCompProps[])Enum.GetValues(typeof(HediffCompProps));
            int propsSize = propsName.Length;

            for (int i = 0; i < propsSize; i++)
                HediffComps_BitMaskDB[propsName[i]] = (int)propsVals[i];
        }

        // This inits the DB for Hediffs
        public static void InitHediffCompsDB()
        {

            // Populate the Hediffs Mask DB
            PopulateHediffEnumDB();

            // Doing the stuff
            List<HediffDef> AllHediffs = DefDatabase<HediffDef>.AllDefsListForReading.Where((x) => (x.comps != null && x.comps.Count > 0)).ToList();
            int hediffAmount = AllHediffs.Count;

            // For each hediff
            for (int i = 0; i < hediffAmount; i++)
            {
                HediffDef def = AllHediffs[i];
                List<HediffCompProperties> comps = def.comps;
                int compsCount = comps.Count;
                int mask = 0;

                for (int j = 0; j < compsCount; j++)
                {
                    mask += HediffComps_BitMaskDB.GetValueSafe(comps[j].GetType().Name);
                }

                // Saves the bit masking
                Hediff_BitMask[def.defName] = mask;
                //bool hasImmune = !(((int)HediffCompProps.HediffCompProperties_Immunizable & mask) == 0);
                //Log.Warning(def.defName + ((hasImmune) ? " has " : " doesn't have ") + "HediffCompProperties_Immunizable");
            }

            Log.Message("Rim73 cached " + Hediff_BitMask.Keys.Count + " heddifs.");
        }

        // Tells you whether this Hediff has this or not
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsHediffType(ref int mask, HediffCompProps type)
        {
            return !(((int)type & mask) == 0);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Tick_1000(ref List<Hediff> hediffs)
        {
            // HediffCompProperties_SeverityPerDay (actually 200 hash interval ticks but 200 is part of 1000) => CompPostTick
            // HediffCompProperties_GrowthMode (actually 5000, but still works), this is only called to change ChangeGrowthMode() to know if we increase or decrease severity
            // SPECIAL CASE PREGNANT HERE

            for (int index = 0; index < hediffs.Count; index++)
            {
                Hediff hediff = hediffs[index];
                int hediffFlag = 0;

                if (!Hediff_BitMask.TryGetValue(hediff.def.defName, out hediffFlag))
                    continue;

                // Special case... ticks every time unfortunately
                if (hediff.def.defName == "Pregnant")
                {
                    float preTickSeverity = hediff.Severity;
                    hediff.Tick();
                    hediff.PostTick();
                    float severityDiff = hediff.Severity - preTickSeverity;
                    hediff.Severity += severityDiff * 1000;
                }

                bool severityPerDay = IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_SeverityPerDay);
                bool growthMode = IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_GrowthMode);

                // GrowthMode is actually SeverityPerDay; just with ChangeGrowthMode() to increase or decrease sevirity
                if (severityPerDay || growthMode)
                {
                    // Get the Severity differences
                    float preTickSeverity = hediff.Severity;
                    hediff.Tick();
                    hediff.PostTick();
                    float severityDiff = hediff.Severity - preTickSeverity;

                    // Simulating 200 x 4 ticks
                    // Because we already ticked once, which simulates 200 ticks
                    hediff.Severity += severityDiff * 3;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Tick_60(ref List<Hediff> hediffs, bool doHediffGivers, ref Pawn pawn)
        {

            // HediffCompProperties_CauseMentalState
            for (int index = 0; index < hediffs.Count; index++)
            {
                Hediff hediff = hediffs[index];
                int hediffFlag = 0;

                if (!Hediff_BitMask.TryGetValue(hediff.def.defName, out hediffFlag))
                    continue;
                    
                if (IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_CauseMentalState))
                {
                    // Simple tick, nothing more
                    hediff.Tick();
                    hediff.PostTick();
                }
            }

            // HediffGivers
            // This is for Hypothermia, Bleeding and Heatstroke
            // They tick 5 times slower now...
            if(doHediffGivers)
            {
                List<HediffGiverSetDef> hediffGiverSets = pawn.RaceProps.hediffGiverSets;
                if (hediffGiverSets != null)
                {
                    int sizeGiversSet = hediffGiverSets.Count;
                    for (int i = 0; i < sizeGiversSet; i++)
                    {
                        List<HediffGiver> hediffGivers = hediffGiverSets[i].hediffGivers;
                        int sizeHediffGivers = hediffGivers.Count;
                        for (int j = 0; j < sizeHediffGivers; j++)
                        {
                            hediffGivers[j].OnIntervalPassed(pawn, (Hediff)null);
                            if (pawn.Dead)
                                return;
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Tick_103(ref List<Hediff> hediffs)
        {
            // HediffCompProperties_Discoverable => CompPostTick
            for (int index = 0; index < hediffs.Count; index++)
            {
                Hediff hediff = hediffs[index];
                int hediffFlag = 0;

                if (!Hediff_BitMask.TryGetValue(hediff.def.defName, out hediffFlag))
                    continue;

                if (IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_Discoverable))
                {
                    // Simple tick, nothing more
                    hediff.Tick();
                    hediff.PostTick();
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Tick_Always(ref List<Hediff> hediffs, ref Pawn pawn, ref ImmunityHandler immunityHandler, int timeDilatation = 300)
        {
            // HediffCompProperties_Disappears
            // HediffCompProperties_TendDuration
            // HediffCompProperties_Immunizable => ImmunityHandlerTick => ImmunityTick => ImmunityChangePerTick
            // HediffCompProperties_HealPermanentWounds
            // HediffCompProperties_MessageAfterTicks => SPECIAL CASE :: Ticks must reach 0 before message is displayed! Do not let ticks go under 0
            // HediffCompProperties_Infecter => SPECIAL CASE :: just like previous, ticks must reach 0 and can't go under 0
            // HediffCompProperties_KillAfterDays
            // HediffCompProperties_SelfHeal

            for (int index = 0; index < hediffs.Count; index++)
            {
                Hediff hediff = hediffs[index];
                int hediffFlag = 0;

                if (!Hediff_BitMask.TryGetValue(hediff.def.defName, out hediffFlag))
                    continue;

                // Immunity
                // TODO :: Make faster
                if (IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_Immunizable))
                {
                    List<ImmunityRecord> immunities = (List<ImmunityRecord>)ImmunityHandler_immunityList.GetValue(immunityHandler);
                    for (int i = 0; i < immunities.Count; i++)
                    {
                        if(immunities[i].hediffDef == hediff.def)
                        {
                            immunities[i].immunity += (immunities[i].ImmunityChangePerTick(pawn, true, hediff) * timeDilatation);
                            immunities[i].immunity = Mathf.Clamp01(immunities[i].immunity);
                        }
                    }
                }

                // OH BOY, LETS GO
                if (
                    IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_Disappears) ||
                    IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_TendDuration) ||
                    IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_HealPermanentWounds) ||
                    IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_MessageAfterTicks) ||
                    IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_Infecter) ||
                    IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_KillAfterDays) ||
                    IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_SelfHeal)
                )
                {
                    HediffWithComps hediffWC = (HediffWithComps)hediff;

                    // Post Tick has the Comps ticks
                    for (int i = 0; i < hediffWC.comps.Count; ++i)
                    {
                        float severityAdjustment = 0.0f;
                        hediffWC.comps[i].CompPostTick(ref severityAdjustment);
                        hediffWC.Severity += severityAdjustment;

                        // Disappears
                        if (IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_Disappears) && (hediffWC.comps[i] is HediffComp_Disappears))
                        {
                            (hediffWC.comps[i] as HediffComp_Disappears).ticksToDisappear -= timeDilatation;
                        }

                        // Tending
                        if (IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_TendDuration) && (hediffWC.comps[i] is HediffComp_TendDuration))
                        {
                            // Special case, can never reach 0
                            if (!(hediffWC.comps[i].props as HediffCompProperties_TendDuration).TendIsPermanent)
                            {
                                int ticks = (hediffWC.comps[i] as HediffComp_TendDuration).tendTicksLeft;
                                int newDiff = ticks - timeDilatation;
                                (hediffWC.comps[i] as HediffComp_TendDuration).tendTicksLeft = (newDiff > 0) ? newDiff : 0;
                            }
                        }

                        // Healing (luciferium)
                        // Unfortunately ticksToHeal is private, so let's access through Reflection
                        if (IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_HealPermanentWounds) && (hediffWC.comps[i] is HediffComp_HealPermanentWounds))
                        {
                            int ticks = (int)HealPermanentWound_ticksToHeal.GetValue(hediffWC.comps[i]);
                            HealPermanentWound_ticksToHeal.SetValue(hediffWC.comps[i], ticks - timeDilatation);
                        }

                        // Message
                        if (IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_MessageAfterTicks) && (hediffWC.comps[i] is HediffComp_MessageAfterTicks))
                        {
                            int ticks = (int)MessageAfterTicks_ticksUntilMessage.GetValue(hediffWC.comps[i]);
                            int newDiff = ticks - timeDilatation;
                            // We'll let it go to the negatives, no need to show messages, otherwise it keeps spamming message
                            // MessageAfterTicks_ticksUntilMessage.SetValue(hediffWC.comps[i], newDiff >= 0 ? newDiff : 0);
                            MessageAfterTicks_ticksUntilMessage.SetValue(hediffWC.comps[i], newDiff);
                        }

                        // Infecter
                        if (IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_Infecter) && (hediffWC.comps[i] is HediffComp_Infecter))
                        {
                            int ticks = (int)Infecter_ticksUntilInfect.GetValue(hediffWC.comps[i]);
                            // Already tried and failed to ma
                            if (ticks < 0)
                                continue;

                            int newDiff = ticks - timeDilatation;
                            Infecter_ticksUntilInfect.SetValue(hediffWC.comps[i], newDiff >= 0 ? newDiff : 1);
                        }


                        // Kill after days
                        if (IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_KillAfterDays) && (hediffWC.comps[i] is HediffComp_KillAfterDays))
                        {
                            int ticks = (int)KillAfterDays_ticksLeft.GetValue(hediffWC.comps[i]);
                            KillAfterDays_ticksLeft.SetValue(hediffWC.comps[i], ticks - timeDilatation);
                        }

                        // Self Heal
                        if (IsHediffType(ref hediffFlag, HediffCompProps.HediffCompProperties_SelfHeal) && (hediffWC.comps[i] is HediffComp_SelfHeal))
                        {
                            (hediffWC.comps[i] as HediffComp_SelfHeal).ticksSinceHeal += timeDilatation;
                        }
                    }
                }
            }
        }


        // Health Ticks
        [HarmonyPatch(typeof(Pawn_HealthTracker), "HealthTick", new Type[] { })]
        static class Pawn_HealthTick
        {
            static bool Prefix(ref Pawn_HealthTracker __instance, ref Pawn ___pawn)
            {
                if (!Rim73_Settings.hediff)
                    return true;

                int thingId = ___pawn.thingIDNumber;
                int ticks = Find.TickManager.TicksGame;
                int hash = ticks + thingId;

                // This takes advantage of memory location and CPU cache by ticking the same data multiple times
                // Special Ticks
                // 1000 Ticks
                if (hash % 1000 == 0)
                    Tick_1000(ref __instance.hediffSet.hediffs);

                // 60 Ticks
                if (hash % 60 == 0)
                    Tick_60(ref __instance.hediffSet.hediffs, (hash % 300 != 0), ref ___pawn);

                // Special 103 ticks
                if (hash % 103 == 0)
                    Tick_103(ref __instance.hediffSet.hediffs);

                // Special time dilatation
                if (hash % 300 == 0)
                {
                    Tick_Always(ref __instance.hediffSet.hediffs, ref ___pawn, ref __instance.immunity, 300);
                    return true;
                }

                // Skipping ticks
                return false;
            }
        }
    }
}
