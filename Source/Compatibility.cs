using System;
using System.Collections.Generic;

using LudeonTK;
using RimWorld;
using Verse;

using HarmonyLib;
using UnityEngine;

namespace recatek.Compatibility
{
    [StaticConstructorOnStartup]
    public class Compatibility_Startup
    {
        static Compatibility_Startup()
        {
            Harmony harmony = new Harmony("rimworld.mod.recatek.compatibility");
            harmony.PatchAll();
        }
    }

    class Compatibility_Mod : Mod
    {
        public Compatibility_Settings Settings { get; }

        public override string SettingsCategory()
        {
            return "recaCompatibility";
        }

        public Compatibility_Mod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<Compatibility_Settings>();
        }
    }

    class Compatibility_Settings : ModSettings
    {
        public float MinValue => -10.0f;
        public float MaxValue => 10.0f;
        public float Step => 0.5f;

        public override void ExposeData()
        {
            base.ExposeData();
        }

        public bool DoSettingsWindowContents(Listing_Standard listing)
        {
            return false;
        }
    }

    class Compatibility_Overrides : GameComponent
    {
        public static string PawnsToKey(Pawn pawn1, Pawn pawn2)
        {
            if (pawn1 == null)
                throw new ArgumentNullException(nameof(pawn1));
            if (pawn2 == null)
                throw new ArgumentNullException(nameof(pawn2));

            var (id1, id2) = (pawn1.thingIDNumber, pawn2.thingIDNumber);
            if (id1 > id2)
                (id1, id2) = (id2, id1);

            return $"{id1}_{id2}";
        }

        private Dictionary<string, float> overrides;

        public Compatibility_Overrides(Game game) : base()
        {
            // Pass
        }

        public bool TryGetOverride(Pawn pawn1, Pawn pawn2, out float value)
        {
            if (overrides == null)
                overrides = new Dictionary<string, float>();

            string key = PawnsToKey(pawn1, pawn2);
            return overrides.TryGetValue(key, out value);
        }

        public void SetOverride(Pawn pawn1, Pawn pawn2, float? value)
        {
            if (overrides == null)
                overrides = new Dictionary<string, float>();

            string key = PawnsToKey(pawn1, pawn2);
            if (value == null)
                overrides.Remove(key);
            else
                overrides[key] = value.Value;
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref overrides, "overrides", LookMode.Value, LookMode.Value);

            if (overrides == null)
                overrides = new Dictionary<string, float>();
        }
    }

    static class Compatibility_DebugTools
    {
        private static bool TryGetPawnAt(IntVec3 c, out Pawn found)
        {
            found = null;

            foreach (Thing thing in Find.CurrentMap.thingGrid.ThingsAt(c))
            {
                if (thing is Pawn pawn)
                {
                    found = pawn;
                    return true;
                }
            }

            return false;
        }

        private static List<DebugMenuOption> Options_Override_Compatibility(Pawn pawn1, Pawn pawn2)
        {
            var overrides = Current.Game.GetComponent<Compatibility_Overrides>();
            List<DebugMenuOption> debugMenuOptionList = new List<DebugMenuOption>();

            if (overrides.TryGetOverride(pawn1, pawn2, out float currentValue))
            {
                debugMenuOptionList.Add(new DebugMenuOption($"(clear override: {currentValue:F1})", DebugMenuOptionMode.Action, () => overrides.SetOverride(pawn1, pawn2, null)));
            }

            const float epsilon = 0.01f;
            var settings = LoadedModManager.GetMod<Compatibility_Mod>().Settings;

            for (float value = settings.MinValue; value <= settings.MaxValue + epsilon; value += settings.Step)
            {
                float capturedValue = value; // Capture the current value for the lambda
                debugMenuOptionList.Add(new DebugMenuOption(value.ToString("F1"), DebugMenuOptionMode.Action, () => overrides.SetOverride(pawn1, pawn2, capturedValue)));
            }

            return debugMenuOptionList;
        }

        [DebugAction(category = "Pawns", name = "T: Override compatibility", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OverrideCompatibility()
        {
            var overrides = Current.Game.GetComponent<Compatibility_Overrides>();
            DebugTool tool = null;

            tool = new DebugTool("First pawn...", () =>
            {
                if (TryGetPawnAt(UI.MouseCell(), out Pawn pawn1))
                {
                    DebugTools.curTool = new DebugTool("Second pawn...",
                    clickAction: () =>
                    {
                        if (TryGetPawnAt(UI.MouseCell(), out Pawn pawn2))
                        {
                            Find.WindowStack.Add(new Dialog_DebugOptionListLister(Options_Override_Compatibility(pawn1, pawn2)));
                        }

                        DebugTools.curTool = null;
                    },
                    onGUIAction: () =>
                    {
                        if (TryGetPawnAt(UI.MouseCell(), out Pawn pawn2))
                        {
                            if (overrides.TryGetOverride(pawn1, pawn2, out float value))
                            {
                                Vector2 vector = Event.current.mousePosition + new Vector2(15f, 30f);
                                Rect rect = new Rect(vector.x, vector.y, 999f, 999f);
                                Text.Font = GameFont.Small;
                                DevGUI.Label(rect, $"Current overridden compatibility: {value:F1}");
                            }
                        }
                    });
                }
            });

            DebugTools.curTool = tool;
        }
    }

    [HarmonyPatch(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.CompatibilityWith))]
    class Patch_Pawn_RelationsTracker_CompatibilityWith
    {
        // public float CompatibilityWith(Pawn otherPawn)
        [HarmonyPostfix]
        private static void Postfix(ref float __result, ref Pawn ___pawn, Pawn otherPawn)
        {
            var overrides = Current.Game.GetComponent<Compatibility_Overrides>();
            if (overrides.TryGetOverride(___pawn, otherPawn, out float value))
                __result = value;
        }
    }

}
