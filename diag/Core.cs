// Clean rebuild of FlyAndQuicksave 1.0.0 (Caleb Orchard), behaviour copied from the original's IL.
// Diagnostic build: adds a passive state watcher (StateWatch.cs) and log-only hooks (DiagHooks.cs).
// No behaviour changes vs the original.
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(BabyStepsQuickSave.Core), "FlyAndQuickSave", "1.0.1-diag3", "Caleb Orchard (rebuild + diag)")]
[assembly: MelonGame]

namespace BabyStepsQuickSave
{
    public class Core : MelonMod
    {
        public static bool flyCamActive;

        PlayerMovement pm;
        DevCheatChaperone dCC;
        DevCheatsGoHere dCGH;
        float flyCamSpeed = 10f;
        float flyCamSpeedMultiplier = 1f;

        public override void OnInitializeMelon()
        {
            var go = new GameObject("DevCheatChaperone_Mod");
            dCC = go.AddComponent<DevCheatChaperone>();
            dCGH = go.AddComponent<DevCheatsGoHere>();
            new HarmonyLib.Harmony("BabyStepsQuickSave.InputOverride").PatchAll(typeof(InputPatches));
            DiagHooks.Install();
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F9)) dCGH.SaveNatePos();
            else if (Input.GetKeyDown(KeyCode.F10)) dCGH.LoadNatePos();
            else if (Input.GetKeyDown(KeyCode.F1))
            {
                if (pm == null) pm = GameObject.Find("Dudest").GetComponent<PlayerMovement>();
                Diff.FirstF1();
                pm.ToggleFlyCam();
                flyCamActive = !flyCamActive;
                StateWatch.Note("F1 pressed, mod flyCamActive=" + flyCamActive);
            }
            else if (Input.GetKeyDown(KeyCode.F11)) Recovery.ReenableMovement();
            else if (Input.GetKeyDown(KeyCode.F12)) Recovery.RestoreAnimSpeed();
            else if (Input.GetKeyDown(KeyCode.F3)) dCGH.TeleportToCutscene();
            else if (Input.GetKey(KeyCode.F4)) dCGH.IncrementCutscene();

            if (flyCamActive && pm != null && pm.flyCam != null)
            {
                HandleFlyCamMovement();
                HandleFlyCamScrollWheel();
            }
            else
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (scroll != 0f && pm != null)
                {
                    if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
                    {
                        if (scroll > 0f) pm.IncreaseFlyCamFOV(); else pm.DecreaseFlyCamFOV();
                    }
                    else
                    {
                        if (scroll > 0f) pm.IncreadFlyCamSpd(); else pm.DecreaseFlyCamSpd();
                    }
                }
            }

            StateWatch.Tick();
            Diff.Tick();
        }

        void HandleFlyCamMovement()
        {
            Vector3 dir = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) dir += Vector3.forward;
            if (Input.GetKey(KeyCode.S)) dir += Vector3.back;
            if (Input.GetKey(KeyCode.A)) dir += Vector3.left;
            if (Input.GetKey(KeyCode.D)) dir += Vector3.right;
            if (Input.GetKey(KeyCode.E)) dir += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) dir += Vector3.down;
            if (dir.magnitude > 1f) dir.Normalize();

            float mult = 1f;
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (shift && ctrl) mult = 7f;
            else if (ctrl) mult = 5f;
            else if (shift) mult = 2f;

            Transform t = pm.flyCam.transform;
            Vector3 vel = t.TransformDirection(dir) * Mathf.Abs(flyCamSpeed * flyCamSpeedMultiplier * mult);
            t.position += vel * Time.deltaTime;
        }

        void HandleFlyCamScrollWheel()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll == 0f) return;
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt))
            {
                if (scroll > 0f) pm.IncreaseFlyCamFOV(); else pm.DecreaseFlyCamFOV();
            }
            else if (scroll > 0f) flyCamSpeedMultiplier = Mathf.Min(flyCamSpeedMultiplier * 1.2f, 10f);
            else flyCamSpeedMultiplier = Mathf.Max(flyCamSpeedMultiplier * 0.8f, 0.1f);
        }
    }

    // While in fly cam, block game input except the mod's keys, mouse look/scroll and left click.
    [HarmonyPatch]
    public static class InputPatches
    {
        static bool Allowed(KeyCode k) =>
            k == KeyCode.W || k == KeyCode.A || k == KeyCode.S || k == KeyCode.D || k == KeyCode.Q || k == KeyCode.E ||
            k == KeyCode.LeftShift || k == KeyCode.RightShift || k == KeyCode.LeftControl || k == KeyCode.RightControl ||
            k == KeyCode.LeftAlt || k == KeyCode.RightAlt ||
            k == KeyCode.F1 || k == KeyCode.F3 || k == KeyCode.F4 || k == KeyCode.F9 || k == KeyCode.F10;

        static bool AllowedDown(KeyCode k) =>
            k == KeyCode.F1 || k == KeyCode.F3 || k == KeyCode.F4 || k == KeyCode.F9 || k == KeyCode.F10;

        [HarmonyPrefix, HarmonyPatch(typeof(Input), nameof(Input.GetKey), typeof(KeyCode))]
        static bool GetKeyPrefix(KeyCode key, ref bool __result)
        {
            if (!Core.flyCamActive || Allowed(key)) return true;
            __result = false; return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Input), nameof(Input.GetKeyDown), typeof(KeyCode))]
        static bool GetKeyDownPrefix(KeyCode key, ref bool __result)
        {
            if (!Core.flyCamActive || AllowedDown(key)) return true;
            __result = false; return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Input), nameof(Input.GetAxis), typeof(string))]
        static bool GetAxisPrefix(string axisName, ref float __result)
        {
            if (!Core.flyCamActive || axisName == "Mouse ScrollWheel" || axisName == "Mouse X" || axisName == "Mouse Y") return true;
            __result = 0f; return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Input), nameof(Input.GetMouseButton), typeof(int))]
        static bool GetMouseButtonPrefix(int button, ref bool __result)
        {
            if (!Core.flyCamActive || button == 0) return true;
            __result = false; return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonDown), typeof(int))]
        static bool GetMouseButtonDownPrefix(int button, ref bool __result)
        {
            if (!Core.flyCamActive || button == 0) return true;
            __result = false; return false;
        }

        [HarmonyPrefix, HarmonyPatch(typeof(Input), nameof(Input.GetMouseButtonUp), typeof(int))]
        static bool GetMouseButtonUpPrefix(int button, ref bool __result)
        {
            if (!Core.flyCamActive || button == 0) return true;
            __result = false; return false;
        }
    }
}
