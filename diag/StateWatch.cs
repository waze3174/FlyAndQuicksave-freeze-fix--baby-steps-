// Passive watcher: logs a line whenever any tracked player/puppet/menu value changes,
// plus a position heartbeat every 5s. Read-only, never writes game state.
using System;
using System.Text;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace BabyStepsQuickSave
{
    public static class StateWatch
    {
        static string last = "";
        static float nextBeat, nextInput;
        static Vector3 lastBeatPos;
        static readonly StringBuilder sb = new StringBuilder(512);

        public static void Note(string msg) =>
            MelonLogger.Msg($"[Diag t={Time.realtimeSinceStartup:F2}] {msg}");

        // Each read is isolated so one bad member can't kill OnUpdate (lesson from v2).
        static void Add(string name, Func<object> read)
        {
            object v;
            try { v = read(); } catch (Exception e) { v = "ERR:" + e.GetType().Name; }
            sb.Append(name).Append('=').Append(v ?? "null").Append(' ');
        }

        public static void Tick()
        {
            var me = PlayerMovement.me;
            if (me == null) return;

            sb.Clear();
            Add("pm.en", () => me.enabled);
            Add("anim.speed", () => me.anim.speed);
            Add("inFly", () => me.inFlyCam);
            Add("modFly", () => Core.flyCamActive);
            Add("cut", () => me.inCutscene);
            Add("exitCut>0", () => me.exitingCutscene > 0f);
            Add("dead", () => me.dead);
            Add("anim.en", () => me.anim.enabled);
            Add("hip.kin", () => me.hipRb.isKinematic);
            Add("hip.sleep", () => me.hipRb.IsSleeping());
            Add("hip.cons", () => me.hipRb.constraints);
            Add("pup.state", () => me.pm.state);
            Add("pup.activeState", () => me.pm.activeState);
            Add("pup.mode", () => me.pm.mode);
            Add("pup.activeMode", () => me.pm.activeMode);
            Add("pup.swMode", () => me.pm.isSwitchingMode);
            Add("pup.swState", () => me.pm.isSwitchingState);
            Add("pup.killing", () => me.pm.isKilling);
            Add("pup.freezeFlag", () => me.pm.freezeFlag);
            Add("pup.teleport", () => me.pm.teleport);
            Add("pup.animOffByStates", () => me.pm.animationDisabledbyStates);
            Add("pup.pinW", () => Math.Round(me.pm.pinWeight, 2));
            Add("pup.muscW", () => Math.Round(me.pm.muscleWeight, 2));
            Add("pup.en", () => me.pm.enabled);
            Add("bp.state", () => me.bp.state);
            Add("bp.en", () => me.bp.enabled);
            Add("fly.locked", () => FlyCam.locked);
            Add("menu.tp", () => Menu.me != null && Menu.me.teleporting);
            Add("menu.paused", () => Menu.me != null && Menu.me.paused);
            Add("menu.loading", () => Menu.me != null && Menu.me.loading);
            Add("timeScale", () => Time.timeScale);

            string cur = sb.ToString();
            Vector3 pos = Vector3.zero;
            try { pos = me.hipRb.position; } catch { }

            if (cur != last)
            {
                Note($"CHANGE pos={pos.x:F2},{pos.y:F2},{pos.z:F2} | {cur}");
                last = cur;
            }
            // Input probe: does the player's input still reach PlayerMovement? (Rewired -> lStick/rStick/trigs)
            try
            {
                float t0 = me.trigs.Length > 0 ? me.trigs[0] : 0f, t1 = me.trigs.Length > 1 ? me.trigs[1] : 0f;
                bool active = me.lStick.magnitude > 0.15f || me.rStick.magnitude > 0.15f || t0 > 0.15f || t1 > 0.15f;
                if (active && Time.realtimeSinceStartup >= nextInput)
                {
                    Note($"INPUT lStick={me.lStick.ToString("F2")} rStick={me.rStick.ToString("F2")} trigs={t0:F2},{t1:F2} pos={pos.x:F2},{pos.y:F2},{pos.z:F2}");
                    nextInput = Time.realtimeSinceStartup + 0.5f;
                }
            }
            catch (Exception e) { if (Time.realtimeSinceStartup >= nextInput) { Note("INPUT probe ERR " + e.GetType().Name); nextInput = Time.realtimeSinceStartup + 5f; } }

            if (Time.realtimeSinceStartup >= nextBeat)
            {
                Note($"beat pos={pos.x:F2},{pos.y:F2},{pos.z:F2} moved={(pos - lastBeatPos).magnitude:F3}");
                lastBeatPos = pos;
                nextBeat = Time.realtimeSinceStartup + 5f;
            }
        }
    }
}
