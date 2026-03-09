using BepInEx;
using UnityEngine;
using LethalLib.Modules;
using System.IO;
using Unity.Netcode;
using System.Collections.Generic;

namespace TetoMod
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(LethalLib.Plugin.ModGUID)]
    public class Plugin : BaseUnityPlugin
    {
        #region Spawn Configuration
        // Spawn weight per level (0 = disabled, 1-50 = rare, 50-100 = common, 100+ = very common)
        private static readonly Dictionary<Levels.LevelTypes, int> SpawnRarityPerLevel = new Dictionary<Levels.LevelTypes, int>
        {
            // Easy Moons
            { Levels.LevelTypes.ExperimentationLevel, 50 },
            { Levels.LevelTypes.AssuranceLevel, 50 },
            { Levels.LevelTypes.VowLevel, 50 },

            // Intermediate Moons
            { Levels.LevelTypes.OffenseLevel, 50 },
            { Levels.LevelTypes.MarchLevel, 50 },
            { Levels.LevelTypes.AdamanceLevel, 50 },

            // Hard Moons
            { Levels.LevelTypes.RendLevel, 50 },
            // { Levels.LevelTypes.DineLevel, 35 },
            { Levels.LevelTypes.TitanLevel, 60 },
            { Levels.LevelTypes.ArtificeLevel, 60 },
            { Levels.LevelTypes.EmbrionLevel, 60 },

            // Modded Moons
            { Levels.LevelTypes.Modded, 50 },
        };
        #endregion

        public void Awake()
        {
            string bundlePath = Path.Combine(Path.GetDirectoryName(Info.Location), "teto.bundle");
            AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);

            if (bundle == null)
            {
                Logger.LogError("Failed to load asset bundle!");
                return;
            }

            GameObject tetoPrefab = bundle.LoadAsset<GameObject>("assets/mods/teto/teto.prefab");

            if (tetoPrefab == null)
            {
                Logger.LogError("Failed to load teto prefab!");
                return;
            }

            SetupLayers(tetoPrefab);
            Item tetoItem = CreateItemProperties(bundle, tetoPrefab);
            SetupScanNode(tetoPrefab);
            SetupNoisemakerProp(bundle, tetoPrefab, tetoItem);
            RegisterNetworkPrefab(tetoPrefab, tetoItem);

            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded successfully!");
        }

        private void SetupLayers(GameObject prefab)
        {
            prefab.layer = LayerMask.NameToLayer("Props");
            foreach (Transform child in prefab.GetComponentsInChildren<Transform>())
            {
                child.gameObject.layer = prefab.layer;
            }
        }

        private Item CreateItemProperties(AssetBundle bundle, GameObject prefab)
        {
            Item item = ScriptableObject.CreateInstance<Item>();

            // DÜZELTME 3: Unity motoru için iç isimlendirme eklendi
            item.name = "FatassTetoItem";

            item.itemName = "Fatass Teto";
            item.spawnPrefab = prefab;
            item.isScrap = true;
            item.syncUseFunction = true;
            item.minValue = 100;
            item.maxValue = 300;
            item.weight = 1.35f;
            item.grabAnim = "HoldLunchbox";
            item.itemIcon = bundle.LoadAsset<Sprite>("assets/mods/teto/teto_icon.png");
            item.requiresBattery = false;
            item.grabSFX = bundle.LoadAsset<AudioClip>("assets/mods/teto/tetograb.wav");
            item.dropSFX = bundle.LoadAsset<AudioClip>("assets/mods/teto/tetodrop.wav");
            item.rotationOffset = new Vector3(0f, -270f, 180f);
            item.positionOffset = new Vector3(0f, 0f, 0f);
            item.restingRotation = new Vector3(-90f, 0f, 0f);
            item.verticalOffset = 0.3f;

            return item;
        }

        private void SetupScanNode(GameObject prefab)
        {
            // Remove existing scan nodes
            foreach (Transform child in prefab.transform)
            {
                if (child.name == "ScanNode")
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            // Create scan node
            GameObject scanNode = new GameObject("ScanNode");
            scanNode.transform.SetParent(prefab.transform, false);
            scanNode.transform.localPosition = Vector3.zero;

            // Use proper layer lookup instead of hardcoded value
            int scanNodeLayer = LayerMask.NameToLayer("ScanNode");
            scanNode.layer = scanNodeLayer != -1 ? scanNodeLayer : 22;

            BoxCollider collider = scanNode.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(0.5f, 0.5f, 0.5f);
            collider.center = Vector3.zero;

            ScanNodeProperties scanProps = scanNode.AddComponent<ScanNodeProperties>();
            scanProps.headerText = "Fatass Teto";
            scanProps.subText = "Value";
            scanProps.nodeType = 2;
            scanProps.maxRange = 13;
            scanProps.minRange = 1;
            scanProps.requiresLineOfSight = true;
            scanProps.creatureScanID = -1;
        }

        private void SetupNoisemakerProp(AssetBundle bundle, GameObject prefab, Item item)
        {
            NoisemakerProp noisemaker = prefab.GetComponent<NoisemakerProp>();
            if (noisemaker == null)
            {
                noisemaker = prefab.AddComponent<NoisemakerProp>();
            }

            noisemaker.itemProperties = item;
            noisemaker.grabbable = true;
            noisemaker.useCooldown = 0.5f;

            // DÜZELTME: mainObjectRenderer kesinlikle MeshRenderer olmak zorunda.
            noisemaker.mainObjectRenderer = prefab.GetComponentInChildren<MeshRenderer>();

            if (noisemaker.mainObjectRenderer == null)
            {
                Logger.LogWarning("DİKKAT: Teto prefab'ında MeshRenderer bulunamadı! Bu eşya senkronizasyonunu bozabilir.");
                // Eğer Teto modeli SkinnedMeshRenderer kullanıyorsa, Unity'e dönüp prefab'ın 
                // içine küçük, görünmez bir Küp (MeshRenderer) eklemeniz gerekebilir.
            }

            // Get colliders but exclude ScanNode trigger colliders
            List<Collider> validColliders = new List<Collider>();
            foreach (Collider col in prefab.GetComponentsInChildren<Collider>())
            {
                if (!col.isTrigger && col.gameObject.name != "ScanNode")
                {
                    validColliders.Add(col);
                }
            }
            noisemaker.propColliders = validColliders.ToArray();

            AudioSource audioSource = prefab.GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = prefab.AddComponent<AudioSource>();
            }

            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.volume = 1f;
            audioSource.maxDistance = 30f;
            noisemaker.noiseAudio = audioSource;

            noisemaker.noiseRange = 15f;
            noisemaker.maxLoudness = 1f;
            noisemaker.minLoudness = 0.8f;
            noisemaker.minPitch = 0.9f;
            noisemaker.maxPitch = 1.1f;

            AudioClip grabClip = bundle.LoadAsset<AudioClip>("assets/mods/teto/tetograb.wav");
            if (grabClip != null)
            {
                noisemaker.noiseSFX = new AudioClip[] { grabClip };
                noisemaker.noiseSFXFar = new AudioClip[] { grabClip };
            }
        }

        private void RegisterNetworkPrefab(GameObject prefab, Item item)
        {
            if (prefab.GetComponent<NetworkObject>() == null)
            {
                prefab.AddComponent<NetworkObject>();
            }

            LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(prefab);

            // DÜZELTME 1: Hatalı foreach döngüsü kaldırıldı ve Dictionary direkt metoda verildi
            LethalLib.Modules.Items.RegisterScrap(item, SpawnRarityPerLevel);
        }
    }

    public static class PluginInfo
    {
        public const string PLUGIN_GUID = "com.kagan.tetomod";
        public const string PLUGIN_NAME = "Fatass Teto";
        public const string PLUGIN_VERSION = "2.5.0";
    }
}