using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceGame.CommonUI.Input
{
    public interface IInputBindingOverrideRepository
    {
        string LoadJson();
        void SaveJson(string json);
    }

    public sealed class PlayerPrefsInputBindingOverrideRepository :
        IInputBindingOverrideRepository
    {
        public const string OverridesKey =
            "SpaceGame.CommonUI.v1.InputBindings";

        public string LoadJson()
        {
            return PlayerPrefs.GetString(OverridesKey, string.Empty);
        }

        public void SaveJson(string json)
        {
            PlayerPrefs.SetString(OverridesKey, json ?? string.Empty);
            PlayerPrefs.Save();
        }
    }

    public static class InputBindingOverrideUtility
    {
        public static string RestoreAndNormalize(
            InputBindingCatalog catalog,
            string json)
        {
            Restore(catalog, json);
            return catalog?.ActionAsset == null
                ? string.Empty
                : catalog.ActionAsset.SaveBindingOverridesAsJson();
        }

        public static void Restore(InputBindingCatalog catalog, string json)
        {
            if (catalog == null || catalog.ActionAsset == null)
            {
                return;
            }

            catalog.ActionAsset.RemoveAllBindingOverrides();
            string compatibleJson = FilterToExistingBindings(
                catalog.ActionAsset,
                json);
            if (!string.IsNullOrWhiteSpace(compatibleJson))
            {
                catalog.ActionAsset.LoadBindingOverridesFromJson(
                    compatibleJson,
                    false);
            }

            catalog.NotifyBindingsChanged();
        }

        public static string FilterToExistingBindings(
            InputActionAsset actionAsset,
            string json)
        {
            if (actionAsset == null || string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            BindingOverrideListJson payload;
            try
            {
                payload = JsonUtility.FromJson<BindingOverrideListJson>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "Saved input bindings are invalid and will be ignored. "
                    + exception.Message);
                return string.Empty;
            }

            if (payload?.bindings == null || payload.bindings.Count == 0)
            {
                return string.Empty;
            }

            var existingBindingIds = new HashSet<Guid>();
            foreach (InputBinding binding in actionAsset.bindings)
            {
                existingBindingIds.Add(binding.id);
            }

            var compatibleBindings = new List<BindingOverrideJson>(
                payload.bindings.Count);
            foreach (BindingOverrideJson bindingOverride in payload.bindings)
            {
                if (bindingOverride != null &&
                    Guid.TryParse(bindingOverride.id, out Guid bindingId) &&
                    existingBindingIds.Contains(bindingId))
                {
                    compatibleBindings.Add(bindingOverride);
                }
            }

            if (compatibleBindings.Count == payload.bindings.Count)
            {
                return json;
            }

            return compatibleBindings.Count == 0
                ? string.Empty
                : JsonUtility.ToJson(new BindingOverrideListJson
                {
                    bindings = compatibleBindings
                });
        }

        [Serializable]
        private sealed class BindingOverrideListJson
        {
            public List<BindingOverrideJson> bindings;
        }

        [Serializable]
        private sealed class BindingOverrideJson
        {
            public string action;
            public string id;
            public string path;
            public string interactions;
            public string processors;
        }
    }
}
