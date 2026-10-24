using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KnockbackArena.EditorTools
{
    // Renders the playground through the game camera (URP) to PNGs, so the in-engine look can be checked
    // without opening the editor:  Unity -batchmode -executeMethod KnockbackArena.EditorTools.CubeCritterSnapshots.Run -snapshotOut <dir>
    public static class CubeCritterSnapshots
    {
        public static void Run()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-snapshotOut");
            var outDir = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Snapshots";
            Directory.CreateDirectory(outDir);
            bool asyncShaders = EditorSettings.asyncShaderCompilation;
            EditorSettings.asyncShaderCompilation = false;   // otherwise the first frame renders with placeholder shaders
            EditorSceneManager.OpenScene(CubeCritterSetup.ScenePath);
            var clips = AssetDatabase.LoadAllAssetsAtPath(CubeCritterSetup.Fbx).OfType<AnimationClip>().ToDictionary(c => c.name, c => c);
            var critters = Object.FindObjectsByType<CritterAnimator>(FindObjectsSortMode.None).OrderBy(c => c.transform.root.name).ToArray();
            var cam = Camera.main;
            DynamicGI.UpdateEnvironment();
            Shot(cam, Path.Combine(outDir, "_warmup.png"));   // first render after opening has no ambient/reflection data yet
            File.Delete(Path.Combine(outDir, "_warmup.png"));

            // 1. gameplay view: player mid-run seen over the shoulder, dummies ahead
            var player = critters.First(c => c.transform.root.name.StartsWith("Player")).transform.root;
            foreach (var c in critters)
                Pose(c.gameObject, c.transform.root == player ? clips["Run"] : clips["Idle"], c.transform.root == player ? 0.13f : 0.9f);
            cam.transform.SetPositionAndRotation(player.position + new Vector3(0.6f, 1.75f, -3.6f), Quaternion.Euler(13f, 0f, 0f));
            Shot(cam, Path.Combine(outDir, "unity_gameplay_view.png"));

            // 2. front line-up close-up (material check), each in a different pose
            var poses = new[] { ("Spit", 0.1f), ("Victory", 1.2f), ("Idle", 0f), ("Taunt", 0.43f) };
            var order = critters.OrderBy(c => c.transform.root.name.StartsWith("Player") ? 0 : 1).ThenBy(c => c.transform.root.name).ToArray();
            for (int k = 0; k < order.Length; k++)
            {
                var r = order[k].transform.root;
                r.SetPositionAndRotation(new Vector3(-2.1f + 1.4f * k, 0f, 0f), Quaternion.Euler(0f, 180f, 0f));
                Pose(order[k].gameObject, clips[poses[k % poses.Length].Item1], poses[k % poses.Length].Item2);
            }
            cam.transform.SetPositionAndRotation(new Vector3(0.9f, 1.6f, -5.6f), Quaternion.Euler(10f, -6f, 0f));
            Shot(cam, Path.Combine(outDir, "unity_lineup.png"));

            // 3. backs: emblems + curls (what players see all match)
            foreach (var c in order) { Pose(c.gameObject, clips["Idle"], 0f); c.transform.root.rotation = Quaternion.identity; }
            cam.transform.SetPositionAndRotation(new Vector3(-0.6f, 1.7f, -5.4f), Quaternion.Euler(11f, 4f, 0f));
            Shot(cam, Path.Combine(outDir, "unity_backs.png"));
            EditorSettings.asyncShaderCompilation = asyncShaders;
            Debug.Log("[CubeCritter] snapshots written to " + Path.GetFullPath(outDir));
        }

        static void Pose(GameObject go, AnimationClip clip, float t)
        {
            clip.SampleAnimation(go, t);   // transforms; edit-mode sampling skips blend shapes, so set those from the curves
            var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
            foreach (var b in AnimationUtility.GetCurveBindings(clip).Where(b => b.propertyName.StartsWith("blendShape.")))
            {
                int i = smr.sharedMesh.GetBlendShapeIndex(b.propertyName.Substring(11));
                if (i >= 0) smr.SetBlendShapeWeight(i, AnimationUtility.GetEditorCurve(clip, b).Evaluate(t));
            }
        }

        static void Shot(Camera cam, string path, int w = 1280, int h = 720)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }
    }
}
