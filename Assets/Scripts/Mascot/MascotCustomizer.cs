using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace CapsuleCharacter
{
    [System.Serializable]
    public class CustomizationData
    {
        public int bodyIndex = 0;
        public int faceIndex = 0;
        public int hatIndex = 0;
        public int sizeStep = 0;
        public int animationIndex = 0;
        public string characterName = "DefaultCharacter";
    }

    public class MascotCustomizer : MonoBehaviour
    {
        private const float SizeStepAmount = 0.05f;
        private const float MinimumScaleMultiplier = 0.05f;
        private const int MinimumSizeStep = -19; // 1.0 - (19 * 0.05) = 0.05

        private static readonly string[] AnimationNames =
        {
            "idle", "feeling", "jump", "fall", "run"
        };

        [Header("Character References")]
        [SerializeField] private SkinnedMeshRenderer characterMeshRenderer;
        [SerializeField] private Transform hatRoot;
        [Tooltip("Transform to scale. If empty, this GameObject's Transform is used.")]
        [SerializeField] private Transform scaleRoot;
        [Tooltip("Animator containing states named idle, feeling, jump, fall and run.")]
        [SerializeField] private Animator characterAnimator;

        [Header("State")]
        public CustomizationData currentData = new CustomizationData();

        private Material[] bodyMaterials;
        private Material[] faceMaterials;
        private GameObject[] hatPrefabs;
        private readonly List<GameObject> spawnedHats = new List<GameObject>();

        private Vector3 baseScale;
        private bool hasCapturedBaseScale;
        private bool isInitialized;

        private string SavePath => Path.Combine(Application.persistentDataPath, "character_customization.json");

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void Start()
        {
            // Load customization and play animations in Start so the Animator and XR rig are fully ready.
            LoadCustomization();
        }

        private void InitializeIfNeeded()
        {
            if (isInitialized)
                return;

            FindDefaultReferences();
            CaptureBaseScaleIfNeeded();
            LoadResourcesData();
            InstantiateHats();

            if (currentData == null)
                currentData = new CustomizationData();

            isInitialized = true;
        }

        private void FindDefaultReferences()
        {
            if (characterMeshRenderer == null)
            {
                var renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
                Transform targetRoot = transform.Find("Armature/Hips/Spine/Spine1/Spine2/Head/customize_objects");
                foreach (var renderer in renderers)
                {
                    if (targetRoot == null || !renderer.transform.IsChildOf(targetRoot))
                    {
                        characterMeshRenderer = renderer;
                        break;
                    }
                }
            }

            if (hatRoot == null)
                hatRoot = transform.Find("Armature/Hips/Spine/Spine1/Spine2/Head/customize_objects");

            if (scaleRoot == null)
                scaleRoot = transform;

            if (characterAnimator == null)
                characterAnimator = GetComponentInChildren<Animator>(true);
        }

        private void CaptureBaseScaleIfNeeded()
        {
            if (scaleRoot == null)
                scaleRoot = transform;

            if (!hasCapturedBaseScale)
            {
                baseScale = scaleRoot.localScale;
                hasCapturedBaseScale = true;
            }
        }

        private void LoadResourcesData()
        {
            bodyMaterials = Resources.LoadAll<Material>("Materials/Body")
                .OrderBy(material => material.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            faceMaterials = Resources.LoadAll<Material>("Materials/Face")
                .OrderBy(material =>
                {
                    string num = System.Text.RegularExpressions.Regex.Match(material.name, @"\d+").Value;
                    return string.IsNullOrEmpty(num) ? int.MaxValue : int.Parse(num);
                })
                .ToArray();

            hatPrefabs = Resources.LoadAll<GameObject>("Prefabs/Hats");
        }

        private void InstantiateHats()
        {
            FindDefaultReferences();
            if (hatRoot == null || hatPrefabs == null)
                return;

            spawnedHats.Clear();
            for (int i = 0; i < hatRoot.childCount; i++)
                spawnedHats.Add(hatRoot.GetChild(i).gameObject);

            if (spawnedHats.Count == 0 && Application.isPlaying)
            {
                foreach (var prefab in hatPrefabs)
                {
                    if (prefab == null)
                        continue;

                    GameObject hatInstance = Instantiate(prefab, hatRoot, false);
                    hatInstance.SetActive(false);
                    spawnedHats.Add(hatInstance);
                }
            }
        }

        public void ApplyVisuals()
        {
            FindDefaultReferences();

            if (characterMeshRenderer != null)
            {
                // Use sharedMaterials to read array references without dirtying assets during scene load.
                var materials = characterMeshRenderer.sharedMaterials;

                if (bodyMaterials != null && bodyMaterials.Length > 0 && materials.Length > 0)
                {
                    currentData.bodyIndex = Mathf.Clamp(currentData.bodyIndex, 0, bodyMaterials.Length - 1);
                    materials[0] = bodyMaterials[currentData.bodyIndex];
                }

                if (faceMaterials != null && faceMaterials.Length > 0 && materials.Length > 1)
                {
                    currentData.faceIndex = Mathf.Clamp(currentData.faceIndex, 0, faceMaterials.Length - 1);
                    materials[1] = faceMaterials[currentData.faceIndex];
                }

                characterMeshRenderer.sharedMaterials = materials;
            }

            ApplySize();

            for (int i = 0; i < spawnedHats.Count; i++)
            {
                if (spawnedHats[i] != null)
                    spawnedHats[i].SetActive(i == currentData.hatIndex);
            }
        }

        #region Navigation & Actions (for UI Buttons)

        public void NextBody() => Cycle(ref currentData.bodyIndex, 1, bodyMaterials != null ? bodyMaterials.Length : 0);
        public void PrevBody() => Cycle(ref currentData.bodyIndex, -1, bodyMaterials != null ? bodyMaterials.Length : 0);

        public void NextFace() => Cycle(ref currentData.faceIndex, 1, faceMaterials != null ? faceMaterials.Length : 0);
        public void PrevFace() => Cycle(ref currentData.faceIndex, -1, faceMaterials != null ? faceMaterials.Length : 0);

        public void NextHat() => Cycle(ref currentData.hatIndex, 1, spawnedHats.Count);
        public void PrevHat() => Cycle(ref currentData.hatIndex, -1, spawnedHats.Count);

        public void NextSize() => ChangeSize(1);
        public void PrevSize() => ChangeSize(-1);

        private void ChangeSize(int direction)
        {
            currentData.sizeStep = Mathf.Max(MinimumSizeStep, currentData.sizeStep + direction);
            ApplySize();
            SaveCustomization();
        }

        private void ApplySize()
        {
            CaptureBaseScaleIfNeeded();
            if (scaleRoot == null)
                return;

            float multiplier = Mathf.Max(MinimumScaleMultiplier, 1f + currentData.sizeStep * SizeStepAmount);
            scaleRoot.localScale = baseScale * multiplier;
        }

        public void NextAnimation() => ChangeAnimation(1);
        public void PrevAnimation() => ChangeAnimation(-1);

        private void ChangeAnimation(int direction)
        {
            currentData.animationIndex = (currentData.animationIndex + direction + AnimationNames.Length) % AnimationNames.Length;
            ApplyCurrentAnimation();
            SaveCustomization();
        }

        private void ApplyCurrentAnimation()
        {
            if (!Application.isPlaying)
                return;

            FindDefaultReferences();
            if (characterAnimator == null || !characterAnimator.isActiveAndEnabled || characterAnimator.runtimeAnimatorController == null || AnimationNames.Length == 0)
                return;

            currentData.animationIndex = Mathf.Clamp(currentData.animationIndex, 0, AnimationNames.Length - 1);
            string animationName = AnimationNames[currentData.animationIndex];
            string layerName = characterAnimator.GetLayerName(0);
            string statePath = string.IsNullOrEmpty(layerName) ? animationName : layerName + "." + animationName;
            int stateHash = Animator.StringToHash(statePath);

            if (!characterAnimator.HasState(0, stateHash))
            {
                Debug.LogWarning($"Animator state '{statePath}' was not found. Check that the Animator Controller has a state with this name on its first layer.", characterAnimator);
                return;
            }

            characterAnimator.Play(statePath, 0, 0f);
        }

        private void Cycle(ref int index, int direction, int maxCount)
        {
            if (maxCount <= 0)
                return;

            index = (index + direction + maxCount) % maxCount;
            ApplyVisuals();
            SaveCustomization();
        }

        public void Randomize()
        {
            if (bodyMaterials != null && bodyMaterials.Length > 0)
                currentData.bodyIndex = UnityEngine.Random.Range(0, bodyMaterials.Length);
            if (faceMaterials != null && faceMaterials.Length > 0)
                currentData.faceIndex = UnityEngine.Random.Range(0, faceMaterials.Length);
            if (spawnedHats.Count > 0)
                currentData.hatIndex = UnityEngine.Random.Range(0, spawnedHats.Count);

            ApplyVisuals();
            SaveCustomization();
        }

        public void ResetCustomization()
        {
            currentData.bodyIndex = 0;
            currentData.faceIndex = 0;
            currentData.hatIndex = 0;
            currentData.sizeStep = 0;
            currentData.animationIndex = 0;

            ApplyVisuals();
            ApplyCurrentAnimation();
            SaveCustomization();
        }

        #endregion

        #region Persistence (JSON)

        public void SaveCustomization()
        {
            if (!Application.isPlaying)
                return;

            try
            {
                string json = JsonUtility.ToJson(currentData, true);
                File.WriteAllText(SavePath, json);
            }
            catch (Exception exception)
            {
                Debug.LogError("MascotCustomizer: Failed to save customization JSON: " + exception.Message, this);
            }
        }

        public void LoadCustomization()
        {
            InitializeIfNeeded();

            try
            {
                if (File.Exists(SavePath))
                {
                    string json = File.ReadAllText(SavePath);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        CustomizationData loadedData = JsonUtility.FromJson<CustomizationData>(json);
                        if (loadedData != null)
                            currentData = loadedData;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("MascotCustomizer: Could not load customization JSON, using defaults. Error: " + exception.Message, this);
            }

            if (currentData == null)
                currentData = new CustomizationData();

            // Older save files do not contain sizeStep or animationIndex; both default to zero.
            currentData.sizeStep = Mathf.Max(MinimumSizeStep, currentData.sizeStep);
            currentData.animationIndex = Mathf.Clamp(currentData.animationIndex, 0, AnimationNames.Length - 1);

            ApplyVisuals();
            ApplyCurrentAnimation();
        }

        #endregion
    }
}