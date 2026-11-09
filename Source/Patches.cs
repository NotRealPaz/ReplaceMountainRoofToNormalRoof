using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ReplaceMountainRoofToNormalRoof;

/// <summary>
/// Rewrite of the original 1.5-era transpiler patches.
///
/// The original build transpiled four vanilla methods by replacing the first call to
/// GridsUtility.Roofed / RoofGrid.RoofAt found in their IL. That is fragile in two ways:
///  1. it silently mis-targets when a game update reshapes the method body, and
///  2. it collides with other mods transpiling the same methods (e.g. gravship mods),
///     because transpilers mutate the same IL in sequence.
///
/// This rewrite expresses the same behavior as pure POSTFIX patches, which compose
/// safely with any other mod's patches and never depend on IL shape. All logic is
/// map-relative (no assumptions about a single "overworld" map), fixing the
/// multi-map/dimension blind spot of the original.
///
/// Pocket maps (Anomaly's void "metal hell", and any modded dimension using the
/// pocket-map system) are excluded entirely: roof replacement is a world-map
/// feature, and those dimensions manage their own special roofs.
/// </summary>
public static class Patches
{
  public static void Patch(Harmony harm)
  {
    harm.PatchAll(typeof(Patches).Assembly);
    Log.Message("[ReplaceMountainRoofToNormalRoof] Patches applied (postfix-only rewrite)");
  }

  private static bool Enabled => ReplaceMountainRoofToNormalRoof.Settings?.enable ?? false;

  /// <summary>Roof replacement only operates on world maps. Pocket maps (the
  /// Anomaly void and similar special dimensions) are excluded.</summary>
  private static bool RoofReplacementAllowed(Map map)
  {
    return map != null && !map.IsPocketMap;
  }

  private static bool IsThickRoofAt(IntVec3 c, Map map)
  {
    if (map == null) return false;
    RoofDef roof = map.roofGrid.RoofAt(c);
    return roof != null && roof.isThickRoof;
  }

  // ------------------------------------------------------------------
  // 1) WorkGiver_BuildRoof.HasJobOnCell
  //    Vanilla bails out when the cell is roofed at all. When the roof is a
  //    thick (mountain) roof inside the build-roof area, re-run the remaining
  //    vanilla conditions and allow the job if they pass.
  // ------------------------------------------------------------------
  [HarmonyPatch(typeof(WorkGiver_BuildRoof), nameof(WorkGiver_BuildRoof.HasJobOnCell))]
  public static class WorkGiver_BuildRoof_HasJobOnCell
  {
    public static void Postfix(Pawn pawn, IntVec3 c, bool forced, ref bool __result)
    {
      if (__result || !Enabled) return;

      Map map = pawn.Map;
      if (!RoofReplacementAllowed(map)) return;
      if (!map.areaManager.BuildRoof[c]) return;
      if (!IsThickRoofAt(c, map)) return;

      // Remaining vanilla conditions, in vanilla order:
      if (!pawn.CanReserve(c, 1, -1, ReservationLayerDefOf.Ceiling, forced)) return;
      if (!pawn.CanReach(c, PathEndMode.Touch, pawn.NormalMaxDanger())
          && TouchableRoofSupportBuilding(c, pawn) == null) return;
      if (!RoofCollapseUtility.WithinRangeOfRoofHolder(c, map)) return;
      if (!RoofCollapseUtility.ConnectedToRoofHolder(c, map, assumeRoofAtRoot: true)) return;

      Thing blocker = RoofUtility.FirstBlockingThing(c, map);
      if (blocker != null)
      {
        __result = RoofUtility.CanHandleBlockingThing(blocker, pawn, forced);
        return;
      }
      __result = true;
    }

    /// <summary>
    /// Mirror of WorkGiver_BuildRoof.BuildingToTouchToBeAbleToBuildRoof (private in
    /// vanilla, so re-expressed through public APIs — avoids reflection and stays
    /// stable across game updates).
    /// </summary>
    private static Building TouchableRoofSupportBuilding(IntVec3 c, Pawn pawn)
    {
      if (c.Standable(pawn.Map)) return null;
      Building edifice = c.GetEdifice(pawn.Map);
      if (edifice == null) return null;
      if (!pawn.CanReach(edifice, PathEndMode.Touch, pawn.NormalMaxDanger())) return null;
      return edifice;
    }
  }

  // ------------------------------------------------------------------
  // 2) JobDriver_BuildRoof.DoWorkFailOn
  //    Vanilla fails the job when the cell is roofed (work already done).
  //    For thick roofs that failure is lifted so the pawn can replace it.
  // ------------------------------------------------------------------
  [HarmonyPatch(typeof(JobDriver_BuildRoof), "DoWorkFailOn")]
  public static class JobDriver_BuildRoof_DoWorkFailOn
  {
    public static void Postfix(JobDriver_BuildRoof __instance, ref bool __result)
    {
      if (!__result || !Enabled) return;

      Map map = __instance.pawn.Map;
      if (!RoofReplacementAllowed(map)) return;
      IntVec3 cell = __instance.job.targetA.Cell;
      if (IsThickRoofAt(cell, map))
      {
        __result = false;
      }
    }
  }

  // ------------------------------------------------------------------
  // 3) JobDriver_BuildRoof.DoEffect
  //    Vanilla skips cells that are already roofed. This postfix performs the
  //    same 3x3 pass for cells still carrying a thick roof, replacing them with
  //    a constructed roof (same gates as vanilla: area, roof-holder range,
  //    blockers; plus the mortar wake-up parity from the vanilla body).
  // ------------------------------------------------------------------
  [HarmonyPatch(typeof(JobDriver_BuildRoof), "DoEffect")]
  public static class JobDriver_BuildRoof_DoEffect
  {
    public static void Postfix(JobDriver_BuildRoof __instance)
    {
      if (!Enabled) return;

      Map map = __instance.pawn.Map;
      if (!RoofReplacementAllowed(map)) return;
      Pawn pawn = __instance.pawn;
      IntVec3 center = __instance.job.targetA.Cell;

      for (int i = 0; i < 9; i++)
      {
        IntVec3 cell = center + GenAdj.AdjacentCellsAndInside[i];
        if (!cell.InBounds(map) || !map.areaManager.BuildRoof[cell]) continue;
        if (!IsThickRoofAt(cell, map)) continue;
        if (!RoofCollapseUtility.WithinRangeOfRoofHolder(cell, map)) continue;
        if (RoofUtility.FirstBlockingThing(cell, map) != null) continue;

        map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
        MoteMaker.PlaceTempRoof(cell, map);

        List<Thing> things = map.thingGrid.ThingsListAt(cell);
        for (int j = 0; j < things.Count; j++)
        {
          Thing thing = things[j];
          if (thing.def.building != null && thing.def.building.IsMortar
              && thing.TryGetComp(out CompWakeUpDormant comp))
          {
            comp.Activate(pawn);
          }
        }
      }
    }
  }

  // ------------------------------------------------------------------
  // 4) AutoBuildRoofAreaSetter.TryGenerateAreaNow
  //    Vanilla only adds unroofed cells to the build-roof area. This postfix
  //    additionally adds cells of the room that carry a thick roof (or, with
  //    forceAddRoofArea, any roofed cell), respecting the no-roof area and
  //    roof-holder range, exactly like the original mod's behavior.
  //
  //    Implemented by iterating room.Cells (public API) rather than vanilla's
  //    private cellsToRoof set: vanilla's set is stale on its early-return
  //    paths, and reading it safely would require replicating its early-return
  //    gates (including tuned constants). Iterating the room directly has no
  //    freshness problem and no magic numbers. Side effect: rooms larger than
  //    vanilla's auto-roof size cap still get thick-roof area near player
  //    walls — lifting the original mod's "room too big" limitation.
  // ------------------------------------------------------------------
  [HarmonyPatch(typeof(AutoBuildRoofAreaSetter), "TryGenerateAreaNow")]
  public static class AutoBuildRoofAreaSetter_TryGenerateAreaNow
  {
    public static void Postfix(AutoBuildRoofAreaSetter __instance, Room room)
    {
      var settings = ReplaceMountainRoofToNormalRoof.Settings;
      if (settings == null || !settings.enable || !settings.autoReplaceThickRoof) return;

      Map map = room.Map;
      if (!RoofReplacementAllowed(map) || room.Dereferenced || room.TouchesMapEdge || room.IsDoorway)
      {
        return;
      }
      if (!RoomHasPlayerRoofHolder(room, map)) return;

      foreach (IntVec3 cell in room.Cells)
      {
        RoofDef roof = map.roofGrid.RoofAt(cell);
        if (roof == null) continue; // unroofed: vanilla already handled it
        if (!settings.forceAddRoofArea && !roof.isThickRoof) continue;
        if (map.areaManager.NoRoof[cell]) continue; // respect remove-roof area
        if (!RoofCollapseUtility.WithinRangeOfRoofHolder(cell, map, assumeNonNoRoofCellsAreRoofed: true)) continue;

        map.areaManager.BuildRoof[cell] = true;
      }
    }

    /// <summary>Mirror of vanilla's border scan: rooms only auto-roof when a border
    /// cell holds a player-owned roof holder (and no foreign/disallowed one).</summary>
    private static bool RoomHasPlayerRoofHolder(Room room, Map map)
    {
      bool hasPlayerHolder = false;
      foreach (IntVec3 borderCell in room.BorderCells)
      {
        Thing holderOrImpassable = borderCell.GetRoofHolderOrImpassable(map);
        if (holderOrImpassable == null) continue;
        if ((holderOrImpassable.Faction != null && holderOrImpassable.Faction != Faction.OfPlayer)
            || (holderOrImpassable.def.building != null && !holderOrImpassable.def.building.allowAutoroof))
        {
          return false;
        }
        if (holderOrImpassable.Faction == Faction.OfPlayer)
        {
          hasPlayerHolder = true;
        }
      }
      return hasPlayerHolder;
    }
  }
}
