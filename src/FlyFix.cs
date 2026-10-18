// Fix for the permanent freeze after a fly-cam click-teleport.
// Diagnosed 2026-10-18 (see ../diag/): when Menu.Teleport runs while in fly cam, the game leaves
// PlayerMovement.enabled = false and Animator.speed = 0 after the teleport, so player input is
// never read again. We restore both once the teleport is done. Other teleports are left alone.
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace BabyStepsQuickSave
{
    public static class FlyFix
    {
        static bool armed;
        static float fixAt = -1f;

        // Prefix on Menu.Teleport: arm only for teleports that start in fly cam.
        static void TeleportPrefix()
        {
            var me = PlayerMovement.me;
            if (me != null && me.inFlyCam) { armed = true; fixAt = -1f; }
        }

        public static void Install(HarmonyLib.Harmony h)
        {
            try
            {
                h.Patch(AccessTools.Method(typeof(Menu), nameof(Menu.Teleport)),
                    prefix: new HarmonyMethod(typeof(FlyFix), nameof(TeleportPrefix)));
            }
            catch (System.Exception e) { MelonLogger.Warning($"[FlyFix] could not hook Menu.Teleport, auto-fix off (F11 still works): {e.Message}"); }
        }

        public static void Tick()
        {
            if (!armed) return;
            if (Menu.me != null && Menu.me.teleporting) return;
            if (fixAt < 0f) { fixAt = Time.realtimeSinceStartup + 0.25f; return; }   // let the teleport's last frame settle
            if (Time.realtimeSinceStartup < fixAt) return;

            armed = false;
            var me = PlayerMovement.me;
            if (me == null || me.dead || me.inCutscene) return;
            Restore(me, "auto");
        }

        // F11: manual fallback.
        public static void Manual()
        {
            var me = PlayerMovement.me;
            if (me != null) Restore(me, "F11");
        }

        static void Restore(PlayerMovement me, string why)
        {
            bool wasEnabled = me.enabled;
            float wasSpeed = me.anim != null ? me.anim.speed : 1f;
            if (wasEnabled && wasSpeed != 0f) return;
            me.enabled = true;
            if (me.anim != null && wasSpeed == 0f) me.anim.speed = 1f;
            MelonLogger.Msg($"[FlyFix] {why}: PlayerMovement.enabled {wasEnabled} -> True, anim.speed {wasSpeed:F2} -> {(me.anim != null ? me.anim.speed : 0f):F2}");
        }
    }
}
