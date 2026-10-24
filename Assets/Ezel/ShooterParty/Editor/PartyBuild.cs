using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShooterParty.EditorTools
{
    // Builds every target into Builds/<target>/ (gitignored). SP_Boot goes first; every other enabled scene in
    // the build settings follows, so teammates' scenes ride along.
    //   batch:  Unity -batchmode -quit -projectPath . -executeMethod ShooterParty.EditorTools.PartyBuild.BatchMac
    // The Pi server is IL2CPP for Linux Arm64, cross-compiled with the com.unity.toolchain.macos-arm64-linux
    // and com.unity.sdk.linux-arm64 packages. The first build after a fresh import can fail with "Unable to find
    // an Linux Sysroot"; running it again works (deploy.sh retries).
    public static class PartyBuild
    {
        public const string Out = "Builds";

        static string[] Scenes() =>
            new[] { ShooterPartySetup.BootScenePath }
                .Concat(EditorBuildSettings.scenes.Where(s => s.enabled && s.path != ShooterPartySetup.BootScenePath).Select(s => s.path))
                .ToArray();

        [MenuItem("ShooterParty/Build/macOS")] public static void Mac() => Build(BuildTarget.StandaloneOSX, StandaloneBuildSubtarget.Player, $"{Out}/macOS/ShooterParty.app");
        [MenuItem("ShooterParty/Build/Windows")] public static void Windows() => Build(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player, $"{Out}/Windows/ShooterParty.exe");
        [MenuItem("ShooterParty/Build/Linux x64")] public static void Linux() { SetLinuxArchitecture("x64"); Build(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Player, $"{Out}/Linux/ShooterParty.x86_64"); }

        [MenuItem("ShooterParty/Build/Pi Server (Linux Arm64)")]
        public static BuildReport PiServer()
        {
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.IL2CPP);
            SetLinuxArchitecture("ARM64");
            try { return Build(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server, $"{Out}/PiServer/ShooterPartyServer"); }
            finally { SetLinuxArchitecture("x64"); }
        }

        [MenuItem("ShooterParty/Build/All")]
        public static void All() { Mac(); Windows(); Linux(); PiServer(); }

        public static void BatchMac() => Exit(Build(BuildTarget.StandaloneOSX, StandaloneBuildSubtarget.Player, $"{Out}/macOS/ShooterParty.app"));
        public static void BatchWindows() => Exit(Build(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player, $"{Out}/Windows/ShooterParty.exe"));
        public static void BatchLinux() { SetLinuxArchitecture("x64"); Exit(Build(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Player, $"{Out}/Linux/ShooterParty.x86_64")); }
        public static void BatchPiServer() => Exit(PiServer());

        static BuildReport Build(BuildTarget target, StandaloneBuildSubtarget sub, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = Scenes(), target = target, subtarget = (int)sub, locationPathName = path, options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[PartyBuild] {target}/{sub} -> {path}: {s.result}, {s.totalErrors} errors, {s.totalSize / (1024 * 1024)} MB, {s.totalTime.TotalSeconds:F0} s");
            return report;
        }

        // Linux player architecture lives in UnityEditor.LinuxStandalone.UserBuildSettings (Linux module), which the
        // ShooterParty editor assembly cannot reference directly.
        static void SetLinuxArchitecture(string name)
        {
            var t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnityEditor.LinuxStandalone.UserBuildSettings")).FirstOrDefault(x => x != null);
            var prop = t?.GetProperty("architecture", BindingFlags.Public | BindingFlags.Static);
            if (prop == null) { Debug.LogWarning("[PartyBuild] Linux build support is not installed"); return; }
            prop.SetValue(null, Enum.Parse(prop.PropertyType, name));
        }

        static void Exit(BuildReport r) => EditorApplication.Exit(r.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
