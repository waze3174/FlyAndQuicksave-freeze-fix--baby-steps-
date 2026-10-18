// Automatic healthy-vs-frozen diff, no keys needed.
//  - Before the first F1 of the session: a rolling snapshot every 2s while healthy (last 3 kept).
//    Fields that change between those healthy snapshots are marked as noise.
//  - On first F1 the baseline (latest healthy snapshot) is frozen.
//  - 3s and 10s after every Menu.Teleport: snapshot, diff vs baseline minus noise, log it.
//  Full snapshots are also written to UserData/FlyDiag/ for offline reading.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace BabyStepsQuickSave
{
    public static class Diff
    {
        static readonly List<Dictionary<string, string>> rolling = new List<Dictionary<string, string>>();
        static Dictionary<string, string> baseline;
        static readonly HashSet<string> noise = new HashSet<string>();
        static float nextRolling;
        static readonly List<(float at, string tag)> pending = new List<(float, string)>();
        static int teleportCount;

        // Objects we descend into from PlayerMovement (by property name). Everything else: primitives only.
        static readonly HashSet<string> descend = new HashSet<string> {
            "pm", "bp", "hipRb", "anim", "flyCam", "rwPlayer", "props", "state", "joint", "rigidbody", "_rigidbody_k__BackingField" };
        static readonly HashSet<string> skip = new HashSet<string> {
            "transform", "gameObject", "Pointer", "ObjectClass", "WasCollected", "hideFlags", "name", "tag" };

        public static void FirstF1()
        {
            if (baseline != null) return;
            if (rolling.Count == 0) { StateWatch.Note("DIFF no healthy snapshot yet, baseline taken now"); rolling.Add(Take()); }
            baseline = rolling[rolling.Count - 1];
            for (int i = 1; i < rolling.Count; i++)
                foreach (var kv in rolling[i])
                    if (!rolling[i - 1].TryGetValue(kv.Key, out var v) || v != kv.Value) noise.Add(kv.Key);
            StateWatch.Note($"DIFF baseline frozen: {baseline.Count} values, {noise.Count} marked as noise from {rolling.Count} healthy snapshots");
            Save("A_baseline", baseline);
        }

        public static void OnTeleport()
        {
            teleportCount++;
            float now = Time.realtimeSinceStartup;
            pending.Add((now + 3f, $"B{teleportCount}_tp+3s"));
            pending.Add((now + 10f, $"C{teleportCount}_tp+10s"));
        }

        public static void Tick()
        {
            float now = Time.realtimeSinceStartup;
            if (baseline == null && PlayerMovement.me != null && !PlayerMovement.me.inFlyCam && now >= nextRolling)
            {
                nextRolling = now + 2f;
                rolling.Add(Take());
                if (rolling.Count > 3) rolling.RemoveAt(0);
            }
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (now < pending[i].at) continue;
                string tag = pending[i].tag; pending.RemoveAt(i);
                Report(tag);
            }
        }

        static void Report(string tag)
        {
            if (baseline == null) { StateWatch.Note($"DIFF {tag}: no baseline (teleport before any F1?)"); return; }
            var cur = Take();
            Save(tag, cur);
            var sb = new StringBuilder();
            int n = 0;
            foreach (var kv in cur.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                if (noise.Contains(kv.Key)) continue;
                baseline.TryGetValue(kv.Key, out var before);
                if (before == kv.Value) continue;
                n++;
                if (n <= 300) sb.Append("\n    ").Append(kv.Key).Append(": ").Append(before ?? "<absent>").Append(" -> ").Append(kv.Value);
            }
            foreach (var k in baseline.Keys)
                if (!cur.ContainsKey(k) && !noise.Contains(k)) { n++; if (n <= 300) sb.Append("\n    ").Append(k).Append(": ").Append(baseline[k]).Append(" -> <absent>"); }
            StateWatch.Note($"DIFF {tag}: {n} values differ from healthy baseline (noise excluded){sb}");
        }

        static void Save(string tag, Dictionary<string, string> snap)
        {
            try
            {
                string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "FlyDiag");
                Directory.CreateDirectory(dir);
                File.WriteAllLines(Path.Combine(dir, tag + ".txt"),
                    snap.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => (noise.Contains(k.Key) ? "~ " : "  ") + k.Key + " = " + k.Value));
            }
            catch (Exception e) { StateWatch.Note($"DIFF save {tag} failed: {e.Message}"); }
        }

        static Dictionary<string, string> Take()
        {
            var d = new Dictionary<string, string>();
            var me = PlayerMovement.me;
            if (me == null) return d;
            Snap(me, "pm.", d, 0);
            if (Menu.me != null) Snap(Menu.me, "menu.", d, 0);
            d["time.timeScale"] = Time.timeScale.ToString("F3");
            d["time.fixedDeltaTime"] = Time.fixedDeltaTime.ToString("F4");
            d["phys.simulationMode"] = Physics.simulationMode.ToString();
            d["phys.autoSyncTransforms"] = Physics.autoSyncTransforms.ToString();
            try
            {
                int i = 0;
                foreach (var m in me.pm.muscles)
                {
                    string p = $"muscle[{i:D2}:{m.name}].";
                    Snap(m, p, d, 1);
                    var rb = m.rigidbody;
                    if (rb != null)
                    {
                        d[p + "rb.isKinematic"] = rb.isKinematic.ToString();
                        d[p + "rb.sleeping"] = rb.IsSleeping().ToString();
                        d[p + "rb.constraints"] = rb.constraints.ToString();
                        d[p + "rb.detectCollisions"] = rb.detectCollisions.ToString();
                        d[p + "rb.useGravity"] = rb.useGravity.ToString();
                        d[p + "rb.mass"] = rb.mass.ToString("F2");
                        d[p + "rb.go.activeInHierarchy"] = rb.gameObject.activeInHierarchy.ToString();
                    }
                    if (m.joint != null)
                    {
                        d[p + "joint.connectedBody"] = m.joint.connectedBody == null ? "null" : m.joint.connectedBody.name;
                        d[p + "joint.enabled"] = m.joint.gameObject.activeInHierarchy.ToString();
                    }
                    i++;
                }
                d["pm.pm.muscles.count"] = i.ToString();
            }
            catch (Exception e) { d["muscles.ERR"] = e.GetType().Name + ": " + e.Message; }
            return d;
        }

        static void Snap(object o, string prefix, Dictionary<string, string> d, int depth)
        {
            if (o == null) { d[prefix.TrimEnd('.')] = "null"; return; }
            PropertyInfo[] props;
            try { props = o.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance); } catch { return; }
            foreach (var p in props)
            {
                if (skip.Contains(p.Name) || !p.CanRead || p.GetIndexParameters().Length != 0) continue;
                Type t = p.PropertyType;
                try
                {
                    if (t == typeof(bool) || t == typeof(int) || t == typeof(long) || t == typeof(uint) || t.IsEnum)
                        d[prefix + p.Name] = Str(p.GetValue(o));
                    else if (t == typeof(float))
                        d[prefix + p.Name] = ((float)p.GetValue(o)).ToString("F3");
                    else if (t == typeof(double))
                        d[prefix + p.Name] = ((double)p.GetValue(o)).ToString("F3");
                    else if (t == typeof(Vector3))
                        d[prefix + p.Name] = ((Vector3)p.GetValue(o)).ToString("F2");
                    else if (t == typeof(Vector2))
                        d[prefix + p.Name] = ((Vector2)p.GetValue(o)).ToString("F2");
                    else if (t == typeof(string))
                        d[prefix + p.Name] = Str(p.GetValue(o));
                    else if (t.Name.StartsWith("Il2CppStructArray"))
                    {
                        var v = p.GetValue(o) as IEnumerable;
                        if (v == null) { d[prefix + p.Name] = "null"; continue; }
                        var items = v.Cast<object>().Take(33).Select(x => x is float f ? f.ToString("F2") : Str(x)).ToList();
                        if (items.Count <= 32 && items.All(x => x.Length < 12)) d[prefix + p.Name] = "[" + string.Join(",", items) + "]";
                        else d[prefix + p.Name + ".len"] = items.Count > 32 ? ">32" : items.Count.ToString();
                    }
                    else if (depth < 2 && descend.Contains(p.Name))
                        Snap(p.GetValue(o), prefix + p.Name + ".", d, depth + 1);
                    else if (!t.IsValueType)
                    {
                        // record only whether reference members are set, so a lost reference shows up
                        object v = p.GetValue(o);
                        bool isNull = v == null || (v is UnityEngine.Object uo && uo == null);
                        d[prefix + p.Name + "?"] = isNull ? "null" : "set";
                    }
                }
                catch (Exception e) { d[prefix + p.Name] = "ERR:" + (e.InnerException ?? e).GetType().Name; }
            }
        }

        static string Str(object v) => v == null ? "null" : v.ToString();
    }
}
