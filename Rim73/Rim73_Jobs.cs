using System;
using HarmonyLib;
using System.Reflection;
using Verse;
using UnityEngine;
using RimWorld;
using System.Runtime.CompilerServices;
using Verse.AI;
using System.Collections.Generic;
using System.Linq;

namespace Rim73
{
    class Rim73_Jobs
    {
        // Basic analysis
        public static Dictionary<string, int> ToilAnalysis = new Dictionary<string, int>();
        public static FieldInfo JobDriver_toils;
        public static int LastDisplayed;

        // Jobs Hashes Constants
        public const UInt64 Job_LayDown = 2679984368201912323;
        public const UInt64 Job_Wait = 18143094375343664642;
        public const UInt64 Job_GotoWander = 16182971203879871751;
        public const UInt64 Job_Wait_MaintainPosture = 4320060248911881604;
        public const UInt64 Job_Goto = 8464247152507782788;
        public const UInt64 Job_OperateDeepDrill = 2313291229225313468;
        public const UInt64 Job_FinishFrame = 17517766472696382783;
        public const UInt64 Job_CutPlant = 8610537457995510270;
        public const UInt64 Job_Sow = 10703666316432435534;
        public const UInt64 Job_Harvest = 15131047947832039728;
        public const UInt64 Job_HarvestDesignated = 11919274952779648864;
        public const UInt64 Job_CutPlantDesignated = 10478525221413762286;
        public const UInt64 Job_Wait_Wander = 12736036780793427890;
        public const UInt64 Job_OperateScanner = 10631271010765328013;
        public const UInt64 Job_Repair = 399292940117311738;
        public const UInt64 Job_FixBrokenDownBuilding = 6045145228811936377;
        public const UInt64 Job_BuildRoof = 5529176735013278407;

        // Used for fast-access on private members (thanks Tynan)
        public static void InitFieldInfos()
        {
            JobDriver_toils = typeof(JobDriver).GetField("toils", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
        }

        // Hash function
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static UInt64 CalculateHash(string read)
        {
            UInt64 hashedValue = 3074457345618258791ul;
            for (int i = 0; i < read.Length; i++)
            {
                hashedValue += (UInt64)read[i];
                hashedValue *= 3074457345618258799ul;
            }
            return hashedValue;
        }

        [HarmonyPatch(typeof(Verse.AI.Pawn_JobTracker), "JobTrackerTick", new Type[] { })]
        static class Pawn_JobsTick
        {
            // ANALYSIS 
            public static void AnalysisToil(ref JobDriver curDriver)
            {
                //List<Toil> toils = (List<Toil>)JobDriver_toils.GetValue(curDriver);
                //Toil currentToil 
                int amount = 0;
                ToilAnalysis.TryGetValue(curDriver.GetType().Name, out amount);
                amount++;
                ToilAnalysis.SetOrAdd(curDriver.GetType().Name, amount);
            }

            public static void PrintResults()
            {
                if(LastDisplayed != Find.TickManager.TicksGame)
                {
                    foreach (KeyValuePair<string, int> item in ToilAnalysis.OrderBy(key => key.Value))
                    {
                        Verse.Log.Warning(item.Key + " > " + item.Value);
                    }

                    Verse.Log.Warning("===================");
                    LastDisplayed = Find.TickManager.TicksGame;
                }
            }


            // Jobs that restart the Pawn
            public static readonly string[] jobsIgnore = { "Wait_Wander", "Wait", "GotoWander" };

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void CleanupCurrentJob(ref Pawn pawn, ref Pawn_JobTracker instance)
            {
                if (instance.curJob == null)
                    return;

                pawn.ClearReservationsForJob(instance.curJob);
                
                if (instance.curDriver != null)
                {
                    instance.curDriver.ended = true;
                    instance.curDriver.Cleanup(JobCondition.Succeeded);
                }
                instance.curDriver = (JobDriver)null;
                Job curJob = instance.curJob;
                instance.curJob = (Job)null;
                pawn.VerifyReservations();
                pawn.stances.CancelBusyStanceSoft();
                
                if (!pawn.Destroyed && pawn.ShouldDropCarriedThingAfterJob(curJob))
                {
                    Thing _ = null;
                   pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out _);
                }
                
                JobMaker.ReturnToPool(curJob);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static LocalTargetInfo RandomWanderPos(ref Pawn ___pawn)
            {
                return RCellFinder.RandomWanderDestFor(___pawn, ___pawn.Position, 12, (Pawn x, IntVec3 a, IntVec3 b) => { return true; }, Danger.None);
            }

            static bool Prefix(ref Verse.AI.Pawn_JobTracker __instance, Pawn ___pawn)
            {
                if (Rim73_Settings.jobs)
                {
                    if (___pawn.Dead || ___pawn.Downed)
                        return false;

                    // Rope for animals (only for players)
                    if (___pawn.RaceProps.Animal && (__instance.curJob == null || __instance.curJob.def.defName == "Wait"))
                        return true;

                    // Manual checks, this prevents from getting TicksGame from memory again and again
                    int thingId = ___pawn.thingIDNumber;
                    int ticks = Find.TickManager.TicksGame;
                    int hash = thingId + ticks;

                    // If attacking, we tick faster
                    if (___pawn.mindState.anyCloseHostilesRecently && hash % 60 == 0)
                        return true;

                    // ThinkTree jobs
                    if (hash % 90 == 0)
                    {
                        // Skip
                        if (___pawn.RaceProps.Animal && !___pawn.mindState.anyCloseHostilesRecently && !(hash % 180 == 0))
                            return false;

                        /* "Wait_Wander", "Wait" */
                        UInt64 jobHashCode = __instance.curJob != null ? CalculateHash(__instance.curJob.def.defName) : 0;

                        if (hash % 240 == 0 && (
                            __instance.curJob == null ||
                            jobHashCode == Job_Wait_Wander ||
                            jobHashCode == Job_Wait
                            )
                        )
                        {
                            CleanupCurrentJob(ref ___pawn, ref __instance);
                            
                            // Mechs have a tendency to get out and smash things up!
                            if (!___pawn.RaceProps.IsMechanoid)
                                __instance.StartJob(JobMaker.MakeJob(JobDefOf.GotoWander, RandomWanderPos(ref ___pawn)), cancelBusyStances: false);
                        }

                        return true;
                    }

                    // Tick driver
                    // TODO :: Optimizations on this func
                    // In a pawn's day
                    // 80% of toil ticks is done by these Jobs 
                    // JobDriver_LayDown | 36%
                    //      > LayDown is ticked every 150 ticks to check if need has changed
                    //      > Tick Action
                    //      > Change Need_Rest.lastRestTick to GameTicks
                    //      > We can throttle this job safely
                    // JobDriver_Wait | 27%
                    //      > We can throttle this JobDriver by a lot too
                    //      > Every toil is useless but ticked anyway
                    // JobDriver_Goto | 17%
                    //      > Not much we can optimize, it doesn't even have a tickAction

                    // To improve runtime performance, we'll use HashCodes (ints)
                    // First I can hardcode them inside the code, no need to jump in memory
                    // To fetch and find the value
                    // Second they're faster to compare than strings
                    // Only downside is : 
                    // Code is unreadable for a human
                    // There's a slight and tiny chance of collision (non unique hashcode)

                    JobDriver curDriver = __instance.curDriver;
                    if (curDriver != null)
                    {
                        Job curJob = curDriver.job;
                        string jobTypeName = curJob.def.defName;
                        UInt64 jobHashCode = CalculateHash(jobTypeName);
                        
                        if (jobHashCode == Job_LayDown)
                        {
                            // LayDown
                            if (hash % 150 == 0 || hash % 211 == 0)
                            {
                                // Compensating for comfort
                                curDriver.DriverTick();
                                if(___pawn.needs.comfort != null)
                                    ___pawn.needs.comfort.lastComfortUseTick = hash + 150;
                            }

                            return false;
                        }else if (jobHashCode == Job_Wait || jobHashCode == Job_Wait_MaintainPosture)
                        {
                            // Wait and Wait_MaintainPosture
                            if (hash % 120 == 0)
                            {
                                curDriver.DriverTick();
                                return true;
                            }

                            return false;
                        }else if (jobHashCode == Job_Goto || jobHashCode == Job_GotoWander)
                        {
                            // Goto and GotoWander
                            return false;
                        }else if(jobHashCode == Job_OperateDeepDrill || jobHashCode == Job_OperateScanner)
                        {
                            // Deep drilling toils don't have any kind of checks to see if Pawn should do something else...
                            curDriver.DriverTick();

                            // This job doesn't contain checks for its tickAction toil, so let's do it ourselves...
                            // Fix for Half cyclydian cycler
                            if ((hash % 211 == 0) && (___pawn.needs.rest != null && ___pawn.needs.rest.CurLevel <= 0.33 || ___pawn.needs.food.CurLevel <= 0.33))
                            {
                                CleanupCurrentJob(ref ___pawn, ref __instance);
                                __instance.StartJob(JobMaker.MakeJob(JobDefOf.GotoWander, RandomWanderPos(ref ___pawn)), cancelBusyStances: false);
                                return true;
                            }
                        }else if (
                            jobHashCode == Job_FinishFrame ||
                            jobHashCode == Job_CutPlant ||
                            jobHashCode == Job_Sow ||
                            jobHashCode == Job_Harvest ||
                            jobHashCode == Job_HarvestDesignated ||
                            jobHashCode == Job_CutPlantDesignated ||
                            jobHashCode == Job_Repair ||
                            jobHashCode == Job_FixBrokenDownBuilding ||
                            jobHashCode == Job_BuildRoof
                            )
                         {
                            // Pawn has finished his building, let's see what else he can do!
                            curDriver.DriverTick();
                            if (curDriver.ended)
                            {
                                CleanupCurrentJob(ref ___pawn, ref __instance);
                                Rim73.JobTracker_TryFindAndStartJob_FastInvoke(__instance, null);
                                return false;
                            }
                        }
                        else
                        {
                            curDriver.DriverTick();
                        }
                    }
                    
                    /*
                    if (curDriver != null)
                        AnalysisToil(ref curDriver);

                    if (Find.TickManager.TicksGame % 10000 == 0)
                        PrintResults();
                    */

                    // SKIP
                    return false;
                } else {
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(Verse.AI.JobDriver), "CheckCurrentToilEndOrFail", new Type[] { })]
        static class JobDriverTick
        {
            static bool Prefix(ref Verse.AI.JobDriver __instance, ref bool __result)
            {
                if (Rim73_Settings.jobs)
                {
                    Job job = __instance.job;
                    if (job == null)
                    {
                        __result = false;
                        return false;
                    }
                        
                    int ticks = Find.TickManager.TicksGame;
                    int thingId = __instance.pawn.thingIDNumber;
                    string jobTypeName = job.def.defName;
                    UInt64 jobHashCode = CalculateHash(jobTypeName);

                    /*
                     Pre-compiled Hashcodes, prevents fetching from memory (like for strings)
                     Clean > 202508053238439936
                     CutPlant > 8610537457995510270
                     Harvest > 15131047947832039728
                     HarvestDesignated > 11919274952779648864
                     CutPlantDesignated > 10478525221413762286
                    */

                    // We're gonna take every single bit of optimisations we can here
                    if ((ticks + thingId) % 20 == 0 ||
                        jobHashCode == 202508053238439936 ||
                        jobHashCode == 8610537457995510270 ||
                        jobHashCode == 15131047947832039728 ||
                        jobHashCode == 11919274952779648864 ||
                        jobHashCode == 10478525221413762286
                    )
                    {
                        return true;
                    }

                    __result = false;
                    return false;
                }

                return true;
            }
        }
    }
}
