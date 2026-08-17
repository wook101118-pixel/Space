#if UNITY_INCLUDE_TESTS
using System;
using System.Linq;
using System.Reflection;
using Dev.NKY.Scripts;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Dev.CSU._02_Scripts.MainMenu.Editor
{
    public sealed class UIButtonSoundInstallationTests
    {
        private static readonly string[] TargetScenePaths =
        {
            "Assets/Dev/CSU/01_Scenes/InGame.unity",
            "Assets/Dev/CSU/01_Scenes/Ending.unity"
        };

        [Test]
        public void TargetScenes_InstallOneSoundHandlerPerActionButton()
        {
            MethodInfo installMethod =
                typeof(MainMenuUIButtonSound).GetMethod(
                    "InstallIntoScene",
                    BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(installMethod, Is.Not.Null);

            SceneSetup[] originalSetup =
                EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (string scenePath in TargetScenePaths)
                {
                    Scene scene = EditorSceneManager.OpenScene(
                        scenePath,
                        OpenSceneMode.Single);
                    Button[] eligibleButtons = FindButtons(scene)
                        .Where(IsEligibleActionButton)
                        .ToArray();
                    Assert.That(
                        eligibleButtons,
                        Is.Not.Empty,
                        scenePath);

                    int installedCount = (int)installMethod.Invoke(
                        null,
                        new object[] { scene });
                    Assert.That(
                        installedCount,
                        Is.EqualTo(eligibleButtons.Length),
                        scenePath);

                    foreach (Button button in eligibleButtons)
                    {
                        Assert.That(
                            button.GetComponents<
                                MainMenuUIButtonSound>(),
                            Has.Length.EqualTo(1),
                            $"{scenePath}: {button.name}");
                    }

                    int secondPassCount = (int)installMethod.Invoke(
                        null,
                        new object[] { scene });
                    Assert.That(
                        secondPassCount,
                        Is.Zero,
                        $"Installer must be idempotent: {scenePath}");

                    foreach (MainMenuUIButtonSound sound in
                        FindSceneComponents<MainMenuUIButtonSound>(scene))
                    {
                        UnityEngine.Object.DestroyImmediate(sound);
                    }
                }
            }
            finally
            {
                RestoreSceneSetupOrCreateEmpty(originalSetup);
            }
        }

        [Test]
        public void SharedUiClips_AreAvailableInResources()
        {
            Assert.That(
                Resources.Load<AudioClip>(
                    "SFX/RocketShooting/UIHover"),
                Is.Not.Null);
            Assert.That(
                Resources.Load<AudioClip>(
                    "SFX/RocketShooting/UIClick"),
                Is.Not.Null);
        }

        private static bool IsEligibleActionButton(Button button)
        {
            return button != null
                && !string.Equals(
                    button.gameObject.name,
                    "Backdrop",
                    StringComparison.OrdinalIgnoreCase)
                && button.GetComponent<UISoundHandler>() == null;
        }

        private static Button[] FindButtons(Scene scene)
        {
            return FindSceneComponents<Button>(scene);
        }

        private static T[] FindSceneComponents<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static void RestoreSceneSetupOrCreateEmpty(
            SceneSetup[] originalSetup)
        {
            if (originalSetup != null
                && originalSetup.Any(setup =>
                    setup.isLoaded && setup.isActive))
            {
                EditorSceneManager.RestoreSceneManagerSetup(
                    originalSetup);
                return;
            }

            EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
        }
    }
}
#endif
