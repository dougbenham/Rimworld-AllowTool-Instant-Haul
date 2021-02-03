using AllowTool;
using HarmonyLib;
using RimWorld;
using Verse;

namespace AllowToolInstantHaul
{
    [StaticConstructorOnStartup]
    static class HarmonyPatches
    {
        static HarmonyPatches()
        {
            var harmony = new Harmony("doug.allowtool.instanthaul");
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(Designator_HaulUrgently), "DesignateThing")]
    public static class Designator_HaulUrgently_DesignateThing_Patch
    {
        [HarmonyPrefix]
        [HarmonyAfter("UnlimitedHugs.AllowTool")]
        public static bool HaulInstantly(ref Thing thing)
        {
            if (DebugSettings.godMode &&
                StoreUtility.TryFindBestBetterStoreCellFor(thing, null, thing.Map, StoragePriority.Unstored, Faction.OfPlayer, out var target, true))
            {
                Map thingMap = thing.Map; // despawning loses Thing's Map

                if (thing.Spawned)
                    thing.DeSpawn();

                if (!GenPlace.TryPlaceThing(thing, target, thingMap, ThingPlaceMode.Direct))
                    thing.Position = target; // forcefully set position if stacking nicely failed

                return false;
            }
            
            return true;
        }
    }
}
