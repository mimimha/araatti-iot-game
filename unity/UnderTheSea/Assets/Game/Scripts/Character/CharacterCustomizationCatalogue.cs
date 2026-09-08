using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderTheSea.Character
{
    public sealed partial class CharacterCustomizationController
    {
        [Flags]
        public enum WearSlot
        {
            Face = 1, Hair = 2, Top = 4, Bottom = 8, Shoes = 16,
            Glasses = 32, Body = 64, Ears = 128, Gloves = 256,
            Socks = 512, Hat = 1024, FaceAccessory = 2048, Outfit = 4096
        }

        [Serializable]
        private sealed class CatalogPart
        {
            public GameObject prefab;
            public WearSlot slot;
            public WearSlot covers;
            public bool skin;
        }

        [Serializable]
        private sealed class SlotBinding
        {
            public WearSlot slot;
            public SkinnedMeshRenderer renderer;
        }

        [SerializeField] private CatalogPart[] catalogParts = Array.Empty<CatalogPart>();
        [SerializeField] private SlotBinding[] slotBindings = Array.Empty<SlotBinding>();
        private readonly Dictionary<WearSlot, GameObject> equippedObjects = new Dictionary<WearSlot, GameObject>();
        private readonly Dictionary<WearSlot, CatalogPart> equippedParts = new Dictionary<WearSlot, CatalogPart>();

        private bool TryApplyCatalogPart(GameObject prefab)
        {
            CatalogPart definition = null;
            foreach (var entry in catalogParts)
                if (entry.prefab == prefab) { definition = entry; break; }
            if (definition == null) return false;

            SkinnedMeshRenderer target = null;
            foreach (var binding in slotBindings)
                if (binding.slot == definition.slot) { target = binding.renderer; break; }
            if (target == null) throw new InvalidOperationException("Missing slot binding: " + definition.slot);
            var sources = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (sources.Length == 0) throw new InvalidOperationException("No mesh in " + prefab.name);

            var replaced = new List<WearSlot>();
            foreach (var old in equippedParts)
                if (old.Key == definition.slot || (old.Value.covers & definition.slot) != 0
                    || (definition.covers & old.Key) != 0)
                    replaced.Add(old.Key);
            foreach (var slot in replaced)
            {
                equippedObjects[slot].SetActive(false);
                Destroy(equippedObjects[slot]);
                equippedObjects.Remove(slot);
                equippedParts.Remove(slot);
            }

            var root = new GameObject(prefab.name + " Equipped");
            root.transform.SetParent(target.transform.parent, false);
            root.transform.localPosition = target.transform.localPosition;
            root.transform.localRotation = target.transform.localRotation;
            root.transform.localScale = target.transform.localScale;
            var bonesByName = new Dictionary<string, Transform>();
            foreach (var bone in target.bones)
                if (bone != null) bonesByName[bone.name] = bone;
            foreach (var source in sources)
            {
                var child = new GameObject(source.name);
                child.transform.SetParent(root.transform, false);
                var renderer = child.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = source.sharedMesh;
                renderer.sharedMaterials = source.sharedMaterials;
                var bones = new Transform[source.bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    if (source.bones[i] == null || !bonesByName.TryGetValue(source.bones[i].name, out bones[i]))
                        throw new InvalidOperationException("Unmapped bone in " + prefab.name);
                }
                renderer.bones = bones;
                renderer.rootBone = target.rootBone;
                renderer.localBounds = source.localBounds;
                renderer.updateWhenOffscreen = true;
                if (definition.skin) ApplySkinMaterial(renderer);
            }
            equippedObjects[definition.slot] = root;
            equippedParts[definition.slot] = definition;
            RefreshCatalogVisibility();
            return true;
        }

        private bool TryUnequipCatalogPart(GameObject prefab)
        {
            CatalogPart definition = null;
            foreach (var entry in catalogParts)
                if (entry.prefab == prefab) { definition = entry; break; }
            if (definition == null
                || !equippedParts.TryGetValue(definition.slot, out CatalogPart equipped)
                || equipped.prefab != prefab)
                return false;

            if (equippedObjects.TryGetValue(definition.slot, out GameObject equippedObject))
            {
                equippedObject.SetActive(false);
                Destroy(equippedObject);
            }
            equippedObjects.Remove(definition.slot);
            equippedParts.Remove(definition.slot);
            RefreshCatalogVisibility();
            return true;
        }

        private bool IsCatalogPartEquipped(GameObject prefab)
        {
            foreach (var entry in equippedParts.Values)
                if (entry.prefab == prefab) return true;
            return false;
        }

        private void RefreshCatalogVisibility()
        {
            WearSlot occupied = 0;
            foreach (var entry in equippedParts.Values) occupied |= entry.slot | entry.covers;
            foreach (var binding in slotBindings)
            {
                if (binding.renderer == null) continue;
                // Optional slots remain empty until selected. Core body/face use the saved preview.
                bool core = binding.slot == WearSlot.Body || binding.slot == WearSlot.Face
                    || binding.slot == WearSlot.Top || binding.slot == WearSlot.Bottom || binding.slot == WearSlot.Shoes;
                binding.renderer.enabled = core && (occupied & binding.slot) == 0;
            }
        }

        private void RefreshCatalogSkin()
        {
            foreach (var entry in equippedParts)
                if (entry.Value.skin)
                    foreach (var renderer in equippedObjects[entry.Key].GetComponentsInChildren<SkinnedMeshRenderer>())
                        ApplySkinMaterial(renderer);
        }
    }
}
