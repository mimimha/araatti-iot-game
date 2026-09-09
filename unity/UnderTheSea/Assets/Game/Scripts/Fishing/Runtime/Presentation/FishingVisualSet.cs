using System;
using System.Collections.Generic;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    [Serializable]
    public sealed class FishingPrefabPlacement
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale = Vector3.one;

        public GameObject Prefab => prefab;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;
        public Vector3 LocalScale => localScale;

        public void Configure(GameObject value, Vector3 position, Vector3 eulerAngles, Vector3 scale)
        {
            prefab = value;
            localPosition = position;
            localEulerAngles = eulerAngles;
            localScale = scale;
        }
    }

    [Serializable]
    public sealed class FishingFishVisualEntry
    {
        [SerializeField] private string visualKey;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private Color tint = Color.white;

        public string VisualKey => visualKey;
        public GameObject Prefab => prefab;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;
        public Vector3 LocalScale => localScale;
        public Color Tint => tint;

        public FishingFishVisualEntry(string key, GameObject value, Vector3 position, Vector3 eulerAngles, Vector3 scale, Color color)
        {
            visualKey = key;
            prefab = value;
            localPosition = position;
            localEulerAngles = eulerAngles;
            localScale = scale;
            tint = color;
        }
    }

    [CreateAssetMenu(fileName = "FishingVisualSet", menuName = "Fishing Mini Game/Visual Set")]
    public sealed class FishingVisualSet : ScriptableObject
    {
        [SerializeField] private string displayName = "Default Fishing Visuals";
        [SerializeField] private FishingPrefabPlacement angler = new FishingPrefabPlacement();
        [SerializeField] private FishingPrefabPlacement environment = new FishingPrefabPlacement();
        [SerializeField] private bool replaceDefaultEnvironment;
        [SerializeField] private Material oceanMaterial;
        [SerializeField] private List<FishingFishVisualEntry> fishVisuals = new List<FishingFishVisualEntry>();

        public string DisplayName => displayName;
        public FishingPrefabPlacement Angler => angler;
        public FishingPrefabPlacement Environment => environment;
        public bool ReplaceDefaultEnvironment => replaceDefaultEnvironment;
        public Material OceanMaterial => oceanMaterial;

        public bool TryGetFish(string visualKey, out FishingFishVisualEntry entry)
        {
            for (int i = 0; i < fishVisuals.Count; i++)
            {
                FishingFishVisualEntry candidate = fishVisuals[i];
                if (candidate != null && string.Equals(candidate.VisualKey, visualKey, StringComparison.OrdinalIgnoreCase))
                {
                    entry = candidate;
                    return entry.Prefab != null;
                }
            }

            entry = null;
            return false;
        }

        public void Configure(
            string setName,
            FishingPrefabPlacement anglerPlacement,
            FishingPrefabPlacement environmentPlacement,
            bool replaceEnvironment,
            Material waterMaterial,
            IEnumerable<FishingFishVisualEntry> entries)
        {
            displayName = string.IsNullOrWhiteSpace(setName) ? "Fishing Visuals" : setName;
            angler = anglerPlacement ?? new FishingPrefabPlacement();
            environment = environmentPlacement ?? new FishingPrefabPlacement();
            replaceDefaultEnvironment = replaceEnvironment;
            oceanMaterial = waterMaterial;
            fishVisuals = entries != null
                ? new List<FishingFishVisualEntry>(entries)
                : new List<FishingFishVisualEntry>();
        }
    }
}
