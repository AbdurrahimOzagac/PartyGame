using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Abd.KnockbackArena.EditorTools
{
    // Sahne dosyası yoksa, script'ler derlenince bir kereliğine otomatik kurar.
    // Sahne bir kez oluştuktan sonra bu hiçbir şey yapmaz.
    static class KnockbackArenaAutoBuild
    {
        const string ScenePath = "Assets/Abd/KnockbackArena/Scenes/KnockbackArena.unity";

        [InitializeOnLoadMethod]
        static void Run()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;

                // Açık sahnede kaydedilmemiş değişiklik varsa dokunma.
                if (SceneManager.GetActiveScene().isDirty)
                {
                    Debug.LogWarning("Knockback Arena otomatik kurulmadı: açık sahnede kaydedilmemiş değişiklik var. " +
                                     "Kaydedip Abd > Knockback Arena > Build Scene menüsünü çalıştır.");
                    return;
                }

                KnockbackArenaBuilder.Build();
            };
        }
    }
}
