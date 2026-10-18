// Manual recovery test keys (diag3). Each does exactly one thing and logs before/after.
// diag2 diff showed that after a fly-cam click-teleport PlayerMovement.enabled stays false
// (and Animator.speed 0), so the player's input is never read again.
using Il2Cpp;

namespace BabyStepsQuickSave
{
    public static class Recovery
    {
        // F11
        public static void ReenableMovement()
        {
            var me = PlayerMovement.me;
            if (me == null) { StateWatch.Note("F11 recovery: PlayerMovement.me is null"); return; }
            bool before = me.enabled;
            me.enabled = true;
            StateWatch.Note($"F11 recovery: PlayerMovement.enabled {before} -> {me.enabled} (anim.speed={me.anim.speed:F2})");
        }

        // F12
        public static void RestoreAnimSpeed()
        {
            var me = PlayerMovement.me;
            if (me == null || me.anim == null) { StateWatch.Note("F12 recovery: no PlayerMovement/anim"); return; }
            float before = me.anim.speed;
            me.anim.speed = 1f;
            StateWatch.Note($"F12 recovery: anim.speed {before:F2} -> {me.anim.speed:F2} (pm.enabled={me.enabled})");
        }
    }
}
