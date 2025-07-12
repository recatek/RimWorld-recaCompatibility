using System;
using Verse;
using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using LudeonTK;

namespace recatek.Compatibility
{
    [StaticConstructorOnStartup]
    public class Compatibility
    {
        static Compatibility()
        {
            Harmony harmony = new Harmony("rimworld.mod.recatek.compatibility");
            harmony.PatchAll();
        }
    }

    public class CompatibilityConfig : Mod
    {
        public float MinValue => -10.0f;
        public float MaxValue => 10.0f;
        public float Step => 0.5f;

        public CompatibilityConfig(ModContentPack content) : base(content)
        {
            // TODO: Load settings and make the above values configurable.
        }
    }

    public class CompatibilityOverrides : GameComponent
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

        public CompatibilityOverrides(Game game) : base()
        {
            // Do nothing
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

    [HarmonyPatch(typeof(Pawn_RelationsTracker), "CompatibilityWith", new Type[] { typeof(Pawn) })]
    public static class CompatibilityWith_Patch
    {
        [HarmonyPostfix]
        private static void CompatibilityWith_Postfix(ref float __result, ref Pawn ___pawn, ref Pawn otherPawn)
        {
            var overrides = Current.Game.GetComponent<CompatibilityOverrides>();
            if (overrides.TryGetOverride(___pawn, otherPawn, out float value))
                __result = value;
        }
    }

    public static class CompatibilityDebugTools
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
            var overrides = Current.Game.GetComponent<CompatibilityOverrides>();
            List<DebugMenuOption> debugMenuOptionList = new List<DebugMenuOption>();

            if (overrides.TryGetOverride(pawn1, pawn2, out float currentValue))
            {
                debugMenuOptionList.Add(new DebugMenuOption($"(clear override: {currentValue:F1})", DebugMenuOptionMode.Action, () => overrides.SetOverride(pawn1, pawn2, null)));
            }

            const float epsilon = 0.01f;
            var config = LoadedModManager.GetMod<CompatibilityConfig>();
            for (float value = config.MinValue; value <= config.MaxValue + epsilon; value += config.Step)
            {
                float capturedValue = value; // Capture the current value for the lambda
                debugMenuOptionList.Add(new DebugMenuOption(value.ToString("F1"), DebugMenuOptionMode.Action, () => overrides.SetOverride(pawn1, pawn2, capturedValue)));
            }

            return debugMenuOptionList;
        }


        [DebugAction("Pawns", "T: Override compatibility", false, false, false, false, false, 0, false, actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OverrideCompatibility()
        {
            DebugTool tool = null;
            
            tool = new DebugTool("First pawn...", () =>
            {
                if (TryGetPawnAt(UI.MouseCell(), out Pawn pawn1))
                {
                    DebugTools.curTool = new DebugTool("Second pawn...", () =>
                    {
                        if (TryGetPawnAt(UI.MouseCell(), out Pawn pawn2))
                        {
                            Find.WindowStack.Add(new Dialog_DebugOptionListLister(Options_Override_Compatibility(pawn1, pawn2)));
                        }

                        DebugTools.curTool = null;
                    });
                }
            });

            DebugTools.curTool = tool;
        }
    }
}
