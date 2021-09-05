using System;
using System.Collections.Generic;
using HarmonyLib;
using System.Reflection;
using Verse;
using UnityEngine;
using RimWorld;

namespace Rim73
{
    [StaticConstructorOnStartup]
    public static class Rim73_Loader
    {
        // Loader
        static Rim73_Loader() {
            
            // HediffComps Caching
            Rim73_Hediff.InitHediffCompsDB();
            Rim73_Hediff.InitFieldInfos();

            // Jobs init
            Rim73_Jobs.InitFieldInfos();
        }

        // On Loading new game
        [HarmonyPatch(typeof(Game), "FinalizeInit", new Type[] { })]
        static class OnLoadedGame
        {
            static void Postfix()
            {
                // Inits the Dictionary for a fixed size in memory.
                Log.Warning("Rim73 > Loaded new game, resetting caching variables");
                Rim73_MindState.InitCache();
                Rim73_Pather.InitRegionCache();
            }
        }

    }

    public class Rim73 : Mod
    {

        public static Rim73_Settings Settings;
        public static string Version = "1.3b";
        
        // Immunity
        public static MethodInfo ImmunityHandler;
        public static FastInvokeHandler ImmunityFastInvoke;

        // Job Tracker
        public static MethodInfo JobTracker_TryFindAndStartJob;
        public static FastInvokeHandler JobTracker_TryFindAndStartJob_FastInvoke;

        // Job Driver
        public static MethodInfo JobDriver_CheckCurrentToilEndOrFail;
        public static FastInvokeHandler JobDriver_CheckCurrentToilEndOrFail_FastInvoke;
        public static MethodInfo JobDriver_TryActuallyStartNextToil;
        public static FastInvokeHandler JobDriver_TryActuallyStartNextToil_FastInvoke;
        public static MethodInfo JobDriver_CanStartNextToilInBusyStance;
        public static FastInvokeHandler JobDriver_CanStartNextToilInBusyStance_FastInvoke;

        // Hediff comp
        public static MethodInfo HediffComp_HealPermanentWounds_CompPostTick;
        public static FastInvokeHandler HediffComp_HealPermanentWounds_CompPostTick_FastInvoke;

        // Ticker
        public static int Ticks = 0;
        

        public Rim73(ModContentPack content) : base(content)
        {
            var harmony = new Harmony("ghost.rolly.rim73");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Get MethodInfo for Immunity Handler
            MethodInfo[] ImmunityHandler_Methods = typeof(ImmunityHandler).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            ImmunityHandler = ImmunityHandler_Methods[ImmunityHandler_Methods.FirstIndexOf((MethodInfo x) => (x.Name == "ImmunityHandlerTick"))];
            ImmunityFastInvoke = MethodInvoker.GetHandler(ImmunityHandler);

            // Methods for JobDriver
            MethodInfo[] JobDriver_Methods = typeof(Verse.AI.JobDriver).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            JobDriver_CheckCurrentToilEndOrFail = JobDriver_Methods[JobDriver_Methods.FirstIndexOf((MethodInfo x) => (x.Name == "CheckCurrentToilEndOrFail"))];
            JobDriver_CheckCurrentToilEndOrFail_FastInvoke = MethodInvoker.GetHandler(JobDriver_CheckCurrentToilEndOrFail);
            JobDriver_TryActuallyStartNextToil = JobDriver_Methods[JobDriver_Methods.FirstIndexOf((MethodInfo x) => (x.Name == "TryActuallyStartNextToil"))];
            JobDriver_TryActuallyStartNextToil_FastInvoke = MethodInvoker.GetHandler(JobDriver_TryActuallyStartNextToil);

            // Methods for JobTracker
            MethodInfo[] JobTracker_Methods = typeof(Verse.AI.Pawn_JobTracker).GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            JobTracker_TryFindAndStartJob = JobTracker_Methods[JobTracker_Methods.FirstIndexOf((MethodInfo x) => (x.Name == "TryFindAndStartJob"))];
            JobTracker_TryFindAndStartJob_FastInvoke = MethodInvoker.GetHandler(JobTracker_TryFindAndStartJob);

            // Methods for HediffComp_HealPermanentWounds
            HediffComp_HealPermanentWounds_CompPostTick = typeof(Verse.HediffComp_HealPermanentWounds).GetMethod("CompPostTick");
            HediffComp_HealPermanentWounds_CompPostTick_FastInvoke = MethodInvoker.GetHandler(HediffComp_HealPermanentWounds_CompPostTick);

            Verse.Log.Message("Rim73 "+ Version + " Initialized");
            base.GetSettings<Rim73_Settings>();
        }
        
        public override string SettingsCategory()
        {
            return "Rim73 - "+ Version;
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Rim73_Settings.DoSettingsWindowContents(inRect);
        }
        
    }
}
