// Log-only Harmony hooks on game methods that could be involved in the freeze.
// Each prefix only logs and returns true (original always runs). Each patch is installed
// separately so a failing one is logged and skipped instead of breaking the mod.
using System;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppRootMotion.Dynamics;
using UnityEngine;

namespace BabyStepsQuickSave
{
    public static class DiagHooks
    {
        static HarmonyLib.Harmony h;

        public static void Install()
        {
            h = new HarmonyLib.Harmony("BabyStepsQuickSave.Diag");
            Hook(typeof(Menu), nameof(Menu.Teleport), nameof(Log_MenuTeleport));
            Hook(typeof(PuppetMaster), nameof(PuppetMaster.Teleport), nameof(Log_PupTeleport));
            Hook(typeof(PuppetMaster), nameof(PuppetMaster.Freeze), nameof(Log_Generic), Type.EmptyTypes);
            Hook(typeof(PuppetMaster), nameof(PuppetMaster.Freeze), nameof(Log_Generic), new[] { typeof(PuppetMaster.StateSettings) });
            Hook(typeof(PuppetMaster), nameof(PuppetMaster.Kill), nameof(Log_Generic), Type.EmptyTypes);
            Hook(typeof(PuppetMaster), nameof(PuppetMaster.Kill), nameof(Log_Generic), new[] { typeof(PuppetMaster.StateSettings) });
            Hook(typeof(PuppetMaster), nameof(PuppetMaster.Resurrect), nameof(Log_Generic));
            Hook(typeof(PuppetMaster), nameof(PuppetMaster.SwitchToActiveMode), nameof(Log_Generic));
            Hook(typeof(PuppetMaster), nameof(PuppetMaster.SwitchToKinematicMode), nameof(Log_Generic));
            Hook(typeof(PuppetMaster), nameof(PuppetMaster.SwitchToDisabledMode), nameof(Log_Generic));
            Hook(typeof(PuppetMaster), nameof(PuppetMaster.SetAnimationEnabled), nameof(Log_SetAnim));
            Hook(typeof(PlayerMovement), nameof(PlayerMovement.ToggleFlyCam), nameof(Log_Generic));
            Hook(typeof(PlayerMovement), nameof(PlayerMovement.Die), nameof(Log_Generic));
            Hook(typeof(PlayerMovement), nameof(PlayerMovement.PrepareToCutscene), nameof(Log_Generic));
            // SetGameplayCam runs every frame: not hooked (it flooded the diag1 log).
        }

        static void Hook(Type t, string method, string prefix, Type[] args = null)
        {
            try
            {
                MethodInfo target = args == null ? AccessTools.Method(t, method) : AccessTools.Method(t, method, args);
                if (target == null) { StateWatch.Note($"hook SKIP {t.Name}.{method}: not found"); return; }
                h.Patch(target, prefix: new HarmonyMethod(typeof(DiagHooks), prefix));
                StateWatch.Note($"hook OK   {t.Name}.{method}");
            }
            catch (Exception e) { StateWatch.Note($"hook FAIL {t.Name}.{method}: {e.GetType().Name} {e.Message}"); }
        }

        static void Log_Generic(MethodBase __originalMethod) =>
            StateWatch.Note($"CALL {__originalMethod.DeclaringType.Name}.{__originalMethod.Name}");

        static void Log_MenuTeleport(Vector3 cp, bool dead)
        {
            StateWatch.Note($"CALL Menu.Teleport cp={cp.x:F2},{cp.y:F2},{cp.z:F2} dead={dead}");
            Diff.OnTeleport();
        }

        static void Log_PupTeleport(Vector3 position, bool moveToTarget) =>
            StateWatch.Note($"CALL PuppetMaster.Teleport pos={position.x:F2},{position.y:F2},{position.z:F2} moveToTarget={moveToTarget}");

        static void Log_SetAnim(bool to) =>
            StateWatch.Note($"CALL PuppetMaster.SetAnimationEnabled({to})");
    }
}
