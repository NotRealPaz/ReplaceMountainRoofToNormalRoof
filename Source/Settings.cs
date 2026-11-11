using Verse;

namespace ReplaceMountainRoofToNormalRoof;

/// <summary>
/// Mod settings. Field names double as the persisted XML keys, so they must not change:
/// existing players' settings carry over from the old build ("enable", "autoReplaceThickRoof",
/// "forceAddRoofArea"). Instance fields (the old build used statics; instance fields are the
/// canonical RimWorld pattern and avoid the settings-not-persisting class of bugs).
/// </summary>
public class Settings : ModSettings
{
  public bool enable = true;
  public bool autoReplaceThickRoof = true;
  public bool forceAddRoofArea = false;

  // All default false: conservative opt-ins over public APIs.
  public bool allowPocketMaps = false;
  public bool allowModdedThickRoofs = false;

  public override void ExposeData()
  {
    base.ExposeData();
    Scribe_Values.Look(ref enable, "enable", true);
    Scribe_Values.Look(ref autoReplaceThickRoof, "autoReplaceThickRoof", true);
    Scribe_Values.Look(ref forceAddRoofArea, "forceAddRoofArea", false);
    Scribe_Values.Look(ref allowPocketMaps, "allowPocketMaps", false);
    Scribe_Values.Look(ref allowModdedThickRoofs, "allowModdedThickRoofs", false);
  }
}
