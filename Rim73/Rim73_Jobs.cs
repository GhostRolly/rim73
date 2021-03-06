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
        public static FieldInfo Need_Rest_lastRestTick;
        public static int LastDisplayed;

        // Jobs Hashes Constants
        public const UInt64 Job_None = 0;
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
        public const UInt64 Job_Clean = 202508053238439936;
        public const UInt64 Job_SpectateCeremony = 16606846453230181852;
        public const UInt64 Job_StandAndBeSociallyActive = 5369201414247730307;
        public const UInt64 Job_GiveSpeech = 3019778364028968580;
        public const UInt64 Job_MarryAdjacentPawn = 6719037904742927402;
        public const UInt64 Job_AttackMelee = 5645113172716386877;
        public const UInt64 Job_LayDownResting = 9748045025245048591;
        public const UInt64 Job_LayDownAwake = 9589447975186139704;
        public const UInt64 Job_Ingest = 5067400492013787987;
        public const UInt64 Job_Deconstruct = 3139963962373897619;
        public const UInt64 Job_RemoveRoof = 16196833210341086447;
        public const UInt64 Job_DoBill = 18364458676350300181;
        public const UInt64 Job_SmoothFloor = 5182257347951032059;
        public const UInt64 Job_Mine = 9147459904847990036;
        public const UInt64 Job_RemoveFloor = 5657975169607793729;
        public const UInt64 Job_SmoothWall = 827729921596434943;
        public const UInt64 Job_HaulToCell = 14690506950397508106;
        public const UInt64 Job_HaulToContainer = 15535361015283980771;
        public const UInt64 Job_PrepareCaravan_GatherItems = 8233455930363745394;


        // Used for fast-access on private members (thanks Tynan)
        public static void InitFieldInfos()
        {
            JobDriver_toils = typeof(JobDriver).GetField("toils", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
            Need_Rest_lastRestTick = typeof(Need_Rest).GetField("lastRestTick", BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance);
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
                    if (___pawn.health.State == PawnHealthState.Dead)
                        return false;

                    // Drafted and Enemies skipped
                    if (___pawn.mindState.anyCloseHostilesRecently)
                        return true;

                    // Hash
                    UInt64 jobHashCode = __instance.curJob != null ? CalculateHash(__instance.curJob.def.defName) : 0;

                    // Rope for animals (only for players)
                    if ((jobHashCode == Job_None || jobHashCode == Job_Wait) && ___pawn.Faction != null && ___pawn.RaceProps.Animal)
                        return true;

                    // Manual checks, this prevents from getting TicksGame from memory again and again
                    int thingId = ___pawn.thingIDNumber;
                    int ticks = Rim73.Ticks;
                    int hash = thingId + ticks;
                    bool isTickingHash = (hash % 120 == 0);
                    bool isLikelyAnimal = ___pawn.Faction == null;

                    // Melee skip
                    if (jobHashCode == Job_AttackMelee)
                    {
                        ___pawn.mindState.anyCloseHostilesRecently = true;
                        return true;
                    }

                    // Animals, ticks really slow
                    if (isLikelyAnimal) {

                        // Once every odd 500 ticks, search for a new job
                        if ((((hash & 540) == 540) && (hash & 1) == 0) && hash % 30 == 0)
                            return true;

                        if ((jobHashCode == Job_Wait || jobHashCode == Job_Wait_MaintainPosture) && (hash & (512-1)) == 0)
                        {
                            CleanupCurrentJob(ref ___pawn, ref __instance);
                            __instance.StartJob(JobMaker.MakeJob(JobDefOf.GotoWander, RandomWanderPos(ref ___pawn)), cancelBusyStances: false);
                        }
                    }

                    // ThinkTree jobs
                    if (isTickingHash && jobHashCode != Job_LayDown && jobHashCode != Job_GotoWander && !isLikelyAnimal)
                    {
                        return true;
                    }

                    // Tick driver
                    // TODO :: Optimizations on this func
                    // In a pawn's day
                    // 80% of toil ticks is done by these Jobs 
                    // JobDriver_LayDown | 36%
                    //      > LayDown is ticked every 150 ticks to check if need has changed
                    //      > Tick Action
                    //      > Change Need_Rest.lastRestTick to GameTick
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
                        if (jobHashCode == Job_LayDown)
                        {
                            // LayDown
                            if (hash % 150 == 0 || hash % 211 == 0)
                            {
                                // Compensating for comfort
                                curDriver.DriverTick();

                                if (___pawn.needs.comfort != null)
                                    ___pawn.needs.comfort.lastComfortUseTick = Rim73.RealTicks + 211;

                                if (___pawn.needs.rest != null)
                                    Need_Rest_lastRestTick.SetValue(___pawn.needs.rest, Rim73.RealTicks + 211);
                            }

                            return false;
                        } else if (jobHashCode == Job_Wait_MaintainPosture || jobHashCode == Job_GotoWander)
                        {
                            // Wait and Wait_MaintainPosture
                            return isTickingHash;
                        } else if (jobHashCode == Job_Goto) {
                            return (___pawn.drafter != null && ___pawn.drafter.Drafted) ? true : false;
                        } else if (
                         jobHashCode == Job_FinishFrame ||
                         jobHashCode == Job_CutPlant ||
                         jobHashCode == Job_Sow ||
                         jobHashCode == Job_Harvest ||
                         jobHashCode == Job_HarvestDesignated ||
                         jobHashCode == Job_CutPlantDesignated ||
                         jobHashCode == Job_Repair ||
                         jobHashCode == Job_FixBrokenDownBuilding ||
                         jobHashCode == Job_BuildRoof ||
                         jobHashCode == Job_Deconstruct ||
                         jobHashCode == Job_RemoveRoof ||
                         jobHashCode == Job_DoBill ||
                         jobHashCode == Job_SmoothFloor ||
                         jobHashCode == Job_Mine ||
                         jobHashCode == Job_RemoveFloor ||
                         jobHashCode == Job_SmoothWall ||
                         jobHashCode == Job_HaulToCell ||
                         jobHashCode == Job_HaulToContainer ||
                         jobHashCode == Job_PrepareCaravan_GatherItems
                      ) {
                            // * NO SKIP JOBS *
                            // While doing these short jobs, pawns don't need to do any kind of checks
                            // We skip a lot of things.
                            // Pawn has finished his building, let's see what else he can do!
                            curDriver.DriverTick();

                            // If job ended, then start new one
                            if (curDriver.ended && !__instance.curJob.playerForced)
                            {
                                CleanupCurrentJob(ref ___pawn, ref __instance);
                                Rim73.JobTracker_TryFindAndStartJob_FastInvoke(__instance, null);
                                return true;
                            }
                        } else {
                            return true;
                        }
                    }

                    /*
                    if (curDriver != null)
                        AnalysisToil(ref curDriver);

                    if (Find.TickManager.TicksGame % 10000 == 0)
                        PrintResults();
                    */

                    // SKIP
                    return jobHashCode == Job_None ? true : false;
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

                    // If enemy then skip
                    if (__instance.pawn.mindState.anyCloseHostilesRecently)
                        return true;

                    //int ticks = Find.TickManager.TicksGame;
                    int ticks = Rim73.Ticks;
                    int thingId = __instance.pawn.thingIDNumber;
                    string jobTypeName = job.def.defName;
                    UInt64 jobHashCode = CalculateHash(jobTypeName);

                    // We're gonna take every single bit of optimisations we can here
                    if (((ticks + thingId) & (16-1)) == 0 ||
                        jobHashCode == Job_Goto ||
                        jobHashCode == Job_Clean ||
                        jobHashCode == Job_CutPlant ||
                        jobHashCode == Job_Harvest ||
                        jobHashCode == Job_HarvestDesignated ||
                        jobHashCode == Job_SpectateCeremony ||
                        jobHashCode == Job_StandAndBeSociallyActive ||
                        jobHashCode == Job_GiveSpeech ||
                        jobHashCode == Job_MarryAdjacentPawn ||
                        jobHashCode == Job_CutPlantDesignated ||
                        jobHashCode == Job_Ingest ||
                        jobHashCode == Job_AttackMelee
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
