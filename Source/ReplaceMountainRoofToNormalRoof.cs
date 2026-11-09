using HarmonyLib;
using UnityEngine;
using Verse;

namespace ReplaceMountainRoofToNormalRoof;

// Note: StaticConstructorOnStartup kept for parity with the original build, but patching
// happens in the Mod constructor (as before), which runs during mod loading.
[StaticConstructorOnStartup]
public class ReplaceMountainRoofToNormalRoof : Mod
{
  public static Settings Settings;

  // Harmony ID intentionally identical to the original build so players updating
  // mid-save never get both the old and new patch sets applied at once.
  public const string HarmonyId = "me.notrealpaz.harmony.replacemountainrooftonormalroof";

  public ReplaceMountainRoofToNormalRoof(ModContentPack content) : base(content)
  {
    Settings = GetSettings<Settings>();
    Harmony harm = new Harmony(HarmonyId);
    Patches.Patch(harm);
  }

  public override void DoSettingsWindowContents(Rect inRect)
  {
    Listing_Standard list = new Listing_Standard();
    list.Begin(inRect);
    list.CheckboxLabeled("Enable".Translate(), ref Settings.enable);
    list.GapLine(12f);
    list.Label("AutoAddBuildRoofAreaExplain".Translate());
    list.CheckboxLabeled("AutoReplaceThickRoof".Translate(), ref Settings.autoReplaceThickRoof);
    list.CheckboxLabeled("ForceAddRoofArea".Translate(), ref Settings.forceAddRoofArea,
      "TooltipForceAddRoofArea".Translate());
    list.End();
    base.DoSettingsWindowContents(inRect);
  }

  public override string SettingsCategory()
  {
    return "ReplaceMountainRoofToNormalRoof".Translate();
  }
}
