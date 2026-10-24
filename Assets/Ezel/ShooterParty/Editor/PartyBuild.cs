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
    // and com.unity.sdk.linux-arm64 packages. The build fails unless the result really is an Arm64 binary.
    public static class PartyBuild
    {
        public const string Out = "Builds";

        static string[] Scenes() =>
            new[] { ShooterPartySetup.BootScenePath }
                .Concat(EditorBuildSettings.scenes.Where(s => s.enabled && s.path != ShooterPartySetup.BootScenePath).Select(s => s.path))
                .ToArray();

        [MenuItem("ShooterParty/Build/macOS")] public static void Mac() => Build(BuildTarget.StandaloneOSX, StandaloneBuildSubtarget.Player, $"{Out}/macOS/ShooterParty.app");
        [MenuItem("ShooterParty/Build/Windows")] public static void Windows() => Build(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player, $"{Out}/Windows/ShooterParty.exe");
        [MenuItem("ShooterParty/Build/Linux x64")] public static void Linux() { UseLinux(StandaloneBuildSubtarget.Player, "x64"); Build(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Player, $"{Out}/Linux/ShooterParty.x86_64"); }

        [MenuItem("ShooterParty/Build/Pi Server (Linux Arm64)")]
        public static bool PiServer()
        {
            const string path = Out + "/PiServer/ShooterPartyServer";
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.IL2CPP);
            UseLinux(StandaloneBuildSubtarget.Server, "ARM64");
            if (Build(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Server, path).summary.result != BuildResult.Succeeded) return false;
            if (IsArm64Elf(path)) return true;
            Debug.LogError($"[PartyBuild] {path} is not an Arm64 binary; the Pi cannot run it");
            return false;
        }

        [MenuItem("ShooterParty/Build/All")]
        public static void All() { Mac(); Windows(); Linux(); PiServer(); }

        public static void BatchMac() => Exit(Build(BuildTarget.StandaloneOSX, StandaloneBuildSubtarget.Player, $"{Out}/macOS/ShooterParty.app"));
        public static void BatchWindows() => Exit(Build(BuildTarget.StandaloneWindows64, StandaloneBuildSubtarget.Player, $"{Out}/Windows/ShooterParty.exe"));
        public static void BatchLinux() { UseLinux(StandaloneBuildSubtarget.Player, "x64"); Exit(Build(BuildTarget.StandaloneLinux64, StandaloneBuildSubtarget.Player, $"{Out}/Linux/ShooterParty.x86_64")); }
        public static void BatchPiServer() => EditorApplication.Exit(PiServer() ? 0 : 1);

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

        // The Linux architecture is stored on the active build profile (player and server have their own), so switch
        // to the profile being built first; otherwise the setting lands on another profile and the build ignores it.
        static void UseLinux(StandaloneBuildSubtarget sub, string architecture)
        {
            EditorUserBuildSettings.standaloneBuildSubtarget = sub;
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
            SetLinuxArchitecture(architecture);
        }

        // ELF header: e_machine at offset 18, 0xB7 = AArch64.
        static bool IsArm64Elf(string path)
        {
            using var f = File.OpenRead(path);
            f.Position = 18;
            return f.ReadByte() == 0xB7 && f.ReadByte() == 0;
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
