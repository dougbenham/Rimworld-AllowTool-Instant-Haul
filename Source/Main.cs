using System.Collections.Generic;
using System.Reflection;
using AllowTool;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

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
        private static readonly MethodInfo _noStorageBlockersInMethod;

        static Designator_HaulUrgently_DesignateThing_Patch()
        {
            _noStorageBlockersInMethod = AccessTools.Method(AccessTools.TypeByName("RimWorld.StoreUtility"), "NoStorageBlockersIn");
        }

        [HarmonyPrefix]
        [HarmonyAfter("UnlimitedHugs.AllowTool")]
        public static bool HaulInstantly(ref Thing thing)
        {
            /*var buildingCount = new Dictionary<Thing, int>();
            var buildingValue = new Dictionary<Thing, float>();
            foreach (var t in thing.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
            {
                if (t.Faction == Faction.OfPlayer)
                {
                    buildingValue[t] = buildingValue.GetValueSafe(t) + t.GetStatValue(StatDefOf.MarketValueIgnoreHp, true);
                    buildingCount[t] = buildingCount.GetValueSafe(t) + 1;
                }
            }
            
            Log.Message("Buildings:");
            foreach (var kv in buildingValue.OrderByDescending(t => t.Value).Take(20))
            {
                Log.Message($"{buildingCount[kv.Key]}x {kv.Key.def.defName}: {kv.Value}");
            }
            
            int num = -1;
            var allDefsListForReading = DefDatabase<TerrainDef>.AllDefsListForReading;
            foreach (var t in allDefsListForReading)
            {
                num = Math.Max(num, t.index);
            }

            var cachedTerrainMarketValueByName = new Dictionary<string, float>();
            foreach (var t in allDefsListForReading)
            {
                var s = t.GetStatValueAbstract(StatDefOf.MarketValue, null);
                cachedTerrainMarketValueByName[t.defName] = s;
            }

            var floorCount = new Dictionary<TerrainDef, int>();
            var floorValue = new Dictionary<TerrainDef, float>();
            var topGrid = thing.Map.terrainGrid.topGrid;
            var fogGrid = thing.Map.fogGrid.fogGrid;
            var size = thing.Map.Size;
            var i = 0;
            int num2 = size.x * size.z;
            while (i < num2)
            {
                if (!fogGrid[i])
                {
                    var t = topGrid[i];
                    floorValue[t] = floorValue.GetValueSafe(t) + cachedTerrainMarketValueByName[t.defName];
                    floorCount[t] = floorCount.GetValueSafe(t) + 1;
                }

                i++;
            }

            Log.Message("Floors:");
            foreach (var kv in floorValue.OrderByDescending(t => t.Value).Take(20))
            {
                Log.Message($"{floorCount[kv.Key]}x {kv.Key.defName}: {kv.Value}");
            }*/
            
            if (DebugSettings.godMode &&
                TryFindBestBetterStoreCellFor(thing, null, thing.Map, StoragePriority.Unstored, Faction.OfPlayer, out var target, true))
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

        public static bool TryFindBestBetterStoreCellFor(Thing t, Pawn carrier, Map map, StoragePriority currentPriority, Faction faction, out IntVec3 foundCell, bool needAccurateResult = true)
        {
            StoragePriority storagePriority = currentPriority;
            float num = 2.14748365E+09f;
            IntVec3 result = IntVec3.Invalid;
            foreach (var slotGroup in map.haulDestinationManager.AllGroupsListInPriorityOrder)
            {
                StoragePriority priority = slotGroup.Settings.Priority;
                if (priority < storagePriority || priority <= currentPriority)
                    break;
                TryFindBestBetterStoreCellForWorker(t, carrier, map, faction, slotGroup, needAccurateResult, ref result, ref num, ref storagePriority);
            }
            if (!result.IsValid)
            {
                foundCell = IntVec3.Invalid;
                return false;
            }
            foundCell = result;
            return true;
        }

        private static void TryFindBestBetterStoreCellForWorker(Thing t, Pawn carrier, Map map, Faction faction, SlotGroup slotGroup, bool needAccurateResult, ref IntVec3 closestSlot, ref float closestDistSquared, ref StoragePriority foundPriority)
        {
            if (slotGroup == null)
            {
                return;
            }
            if (!slotGroup.parent.Accepts(t))
            {
                return;
            }
            IntVec3 a = t.SpawnedOrAnyParentSpawned ? t.PositionHeld : carrier.PositionHeld;
            int count = slotGroup.CellsList.Count;
            var num = needAccurateResult ? Mathf.FloorToInt(count * Rand.Range(0.005f, 0.018f)) : 0;
            for (int i = 0; i < count; i++)
            {
                var slot = slotGroup.CellsList[i];
                float distanceSquared = (a - slot).LengthHorizontalSquared;
                if (distanceSquared <= closestDistSquared && IsGoodStoreCell(slot, map, t, carrier, faction))
                {
                    closestSlot = slot;
                    closestDistSquared = distanceSquared;
                    foundPriority = slotGroup.Settings.Priority;
                    if (i >= num)
                    {
                        break;
                    }
                }
            }
        }

        public static bool IsGoodStoreCell(IntVec3 c, Map map, Thing t, Pawn carrier, Faction faction)
        {
            if (carrier != null && c.IsForbidden(carrier))
            {
                return false;
            }
            
            if (!(bool) _noStorageBlockersInMethod.Invoke(null, new object[] { c, map, t }))
            {
                Log.Warning("Storage blockers found at " + c + " for " + t);
                return false;
            }
            if (carrier != null)
            {
                if (!carrier.CanReserveNew(c))
                {
                    Log.Warning("Carrier " + carrier + " can't reserve new at " + c);
                    return false;
                }
            }
            else if (faction != null)
            {
                var pawn = GetReservationClaimant(map.reservationManager, c, faction);
                if (pawn != null)
                {
                    // Pawn Drone, ProjectRimFactory.Drones.Pawn_Drone, PRFDrone, Dorks
                    Log.Warning("Pawn " + pawn + ", " + pawn.def.thingClass.FullName + ", " + pawn.def.defName + ", " + pawn.Faction + " already reserved " + c);
                    //return false;
                }
            }
            if (c.ContainsStaticFire(map))
            {
                Log.Message("Fire at " + c);
                return false;
            }
            List<Thing> thingList = c.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                if (thingList[i] is IConstructible && GenConstruct.BlocksConstruction(thingList[i], t))
                {
                    Log.Warning("BlocksConstruction at " + c);
                    return false;
                }
            }
            return carrier == null || carrier.Map.reachability.CanReach(t.SpawnedOrAnyParentSpawned ? t.PositionHeld : carrier.PositionHeld, c, PathEndMode.ClosestTouch, TraverseParms.For(carrier, Danger.Deadly, TraverseMode.ByPawn, false, false, false));
        }
        
        public static Pawn GetReservationClaimant(ReservationManager reservationManager, LocalTargetInfo target, Faction faction)
        {
            if (!target.IsValid)
            {
                return null;
            }
            foreach (var reservation in reservationManager.ReservationsReadOnly)
            {
                if (reservation.Target == target && reservation.Claimant.Faction == faction)
                {
                    return reservation.Claimant;
                }
            }
            return null;
        }
    }
}
