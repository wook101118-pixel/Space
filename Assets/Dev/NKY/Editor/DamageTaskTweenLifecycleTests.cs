using System.Reflection;
using DG.Tweening;
using Dev.NKY.Scripts.Health;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Dev.NKY.Editor.Tests
{
    public sealed class DamageTaskTweenLifecycleTests
    {
        private static readonly BindingFlags InstancePrivate =
            BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void Disable_ReleasesHealthUiSequenceReference()
        {
            GameObject root = new GameObject("DamageTask Test");
            HealthDataSo data = ScriptableObject.CreateInstance<HealthDataSo>();
            root.SetActive(false);

            try
            {
                data.maxHealth = 100f;
                data.currentHealth = 100f;

                TestDamageTask task = root.AddComponent<TestDamageTask>();
                Slider foreground = CreateSlider(root.transform, "Foreground");
                Slider background = CreateSlider(root.transform, "Background");

                SetPrivateField(task, "<Data>k__BackingField", data);
                SetPrivateField(task, "healthSlider", foreground);
                SetPrivateField(task, "bgHealthSlider", background);

                root.SetActive(true);
                task.SetUi(100f, 50f);

                Sequence sequence =
                    (Sequence)GetPrivateField(task, "healthUiSequence");
                Assert.That(sequence, Is.Not.Null);
                Assert.That(sequence.IsActive(), Is.True);

                task.InvokeDisableForTest();

                Assert.That(
                    GetPrivateField(task, "healthUiSequence"),
                    Is.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(data);
            }
        }

        [Test]
        public void DisabledDamageSystem_IgnoresDamageAndDeath()
        {
            GameObject root = new GameObject("Damage Suppression Test");
            HealthDataSo data = ScriptableObject.CreateInstance<HealthDataSo>();
            root.SetActive(false);

            try
            {
                data.maxHealth = 100f;
                data.currentHealth = 100f;

                TestDamageTask task = root.AddComponent<TestDamageTask>();
                SetPrivateField(task, "<Data>k__BackingField", data);
                root.SetActive(true);
                task.HealthInit();

                DamageTask.SetDamageSystemEnabled(false);
                task.TakeDamage(100f);

                Assert.That(task.CurrentHealth, Is.EqualTo(100f));
                Assert.That(task.IsDead, Is.False);
            }
            finally
            {
                DamageTask.SetDamageSystemEnabled(true);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(data);
            }
        }

        private static Slider CreateSlider(Transform parent, string name)
        {
            GameObject sliderObject =
                new GameObject(name, typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            return sliderObject.GetComponent<Slider>();
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            FieldInfo field = typeof(DamageTask).GetField(
                fieldName,
                InstancePrivate);
            Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
            return field.GetValue(target);
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = typeof(DamageTask).GetField(
                fieldName,
                InstancePrivate);
            Assert.That(field, Is.Not.Null, $"Missing field: {fieldName}");
            field.SetValue(target, value);
        }

        private sealed class TestDamageTask : DamageTask
        {
            public void InvokeDisableForTest()
            {
                base.OnDisable();
            }
        }
    }
}
