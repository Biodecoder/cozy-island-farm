#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CozyFarm.Core;
using CozyFarm.Farming;
using CozyFarm.Player;
using CozyFarm.Testing;
using CozyFarm.UI;
using CozyFarm.World;

namespace CozyFarm.Editor
{
    public static class FarmSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/CozyIslandFarm.unity";

        [MenuItem("CozyFarm/Build Island Scene & Verify")]
        public static void BuildSceneAndVerify()
        {
            AssetDatabase.Refresh();
            ConfigureTripoModelImporters();
            BuildCozyIslandScene();

            var report = FarmAutopilotPlaytest.RunDeterministicSimulationVerify();
            Debug.Log($"[AUTOPILOT VERIFY] Passed={report.allChecksPassed}, Earned={report.finalCoins - report.initialCoins}G, Seasons={report.verifiedSeasons}");

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            ConfigureBuildSettings();
            BuildStandaloneMacApp();
        }

        private static void ConfigureTripoModelImporters()
        {
            string modelsDir = "Assets/Resources/TripoModels";
            if (!Directory.Exists(modelsDir)) return;

            string[] fbxFiles = Directory.GetFiles(modelsDir, "*.fbx");
            foreach (string fbxPath in fbxFiles)
            {
                string assetPath = fbxPath.Replace("\\", "/");
                ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer != null)
                {
                    importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                    importer.SaveAndReimport();
                }
            }
        }

        private static Material GetOrCreateTripoMaterial(string modelName, Color fallbackColor)
        {
            Directory.CreateDirectory("Assets/Resources/Materials");
            string matPath = $"Assets/Resources/Materials/Tripo_{modelName}_Mat.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            if (m == null)
            {
                m = new Material(sh) { name = $"Tripo_{modelName}_Mat" };
                AssetDatabase.CreateAsset(m, matPath);
            }
            m.shader = sh;
            Texture2D albedo = Resources.Load<Texture2D>($"TripoModels/{modelName}_albedo");
            if (albedo != null)
            {
                m.mainTexture = albedo;
                m.color = Color.white;
            }
            else
            {
                m.color = fallbackColor;
            }
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.18f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static GameObject InstantiateTripoModelOrFallback(
            string modelName,
            string instanceName,
            Vector3 position,
            Vector3 eulerRot,
            float targetHeight,
            Color fallbackColor)
        {
            GameObject prefab = Resources.Load<GameObject>($"TripoModels/{modelName}");
            GameObject instance;
            if (prefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = instanceName;
                instance.transform.position = position;
                instance.transform.rotation = Quaternion.Euler(eulerRot);

                Material pbrMat = GetOrCreateTripoMaterial(modelName, fallbackColor);
                foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = pbrMat;
                }

                // Normalize scale in Unity if importer unit conversion altered height
                Bounds b = ComputeHierarchyBounds(instance);
                if (b.size.y > 0.001f)
                {
                    float s = targetHeight / b.size.y;
                    instance.transform.localScale = instance.transform.localScale * s;
                    b = ComputeHierarchyBounds(instance);
                    float yAdjust = position.y - b.min.y;
                    instance.transform.position += new Vector3(0f, yAdjust, 0f);
                }
            }
            else
            {
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.name = instanceName;
                instance.transform.position = position + new Vector3(0f, targetHeight * 0.5f, 0f);
                instance.transform.rotation = Quaternion.Euler(eulerRot);
                instance.transform.localScale = new Vector3(targetHeight * 0.6f, targetHeight, targetHeight * 0.6f);
                ApplyColor(instance, fallbackColor);
            }
            return instance;
        }

        private static Bounds ComputeHierarchyBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(root.transform.position, Vector3.one);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static Material CreateLitMaterial(string name, Color color, float smoothness = 0.15f)
        {
            Directory.CreateDirectory("Assets/Resources/Materials");
            string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
            string colorHex = ColorUtility.ToHtmlStringRGBA(color);
            string matPath = $"Assets/Resources/Materials/{safeName}_{colorHex}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            Shader sh = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
            if (m == null)
            {
                m = new Material(sh) { name = safeName, color = color };
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
                AssetDatabase.CreateAsset(m, matPath);
            }
            else
            {
                m.shader = sh;
                m.color = color;
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
                EditorUtility.SetDirty(m);
            }
            return m;
        }

        private static void ApplyColor(GameObject go, Color c, float smoothness = 0.15f)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = CreateLitMaterial(go.name + "_Mat", c, smoothness);
            }
        }

        public static void BuildCozyIslandScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 1. Core Game Managers
            GameObject managersRoot = new GameObject("CozyFarmManagers");
            CozyFarmState state = managersRoot.AddComponent<CozyFarmState>();
            FarmPlotGrid plotGrid = managersRoot.AddComponent<FarmPlotGrid>();
            DayNightSeasonController dayNight = managersRoot.AddComponent<DayNightSeasonController>();
            managersRoot.AddComponent<CozyFarmHud>();
            managersRoot.AddComponent<FarmAutopilotPlaytest>();

            // 2. Main Camera & Directional Sun
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            Camera cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.87f, 0.93f);
            cam.fieldOfView = 36f;
            camObj.transform.position = new Vector3(0f, 11.8f, -9.6f);
            camObj.transform.LookAt(new Vector3(0f, 0.6f, 1.4f));

            GameObject sunObj = new GameObject("Directional Light");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.96f, 0.86f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sunObj.transform.rotation = Quaternion.Euler(46f, -34f, 0f);

            dayNight.mainCamera = cam;
            dayNight.directionalSun = sun;

            // 3. Coastal Island Terrain, Sandy Rim & Ocean Water
            GameObject worldRoot = new GameObject("IslandWorld");

            GameObject ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ocean.name = "OceanWater";
            ocean.transform.SetParent(worldRoot.transform);
            ocean.transform.position = new Vector3(0f, -0.42f, 0f);
            ocean.transform.localScale = new Vector3(14f, 1f, 14f);
            ApplyColor(ocean, new Color(0.32f, 0.76f, 0.80f), 0.65f);
            dayNight.oceanWaterRenderer = ocean.GetComponent<Renderer>();

            GameObject sandRim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sandRim.name = "SandyBeachRim";
            sandRim.transform.SetParent(worldRoot.transform);
            sandRim.transform.position = new Vector3(0f, -0.32f, 0.5f);
            sandRim.transform.localScale = new Vector3(26.5f, 0.25f, 22.8f);
            ApplyColor(sandRim, new Color(0.91f, 0.78f, 0.55f));

            GameObject grassIsland = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            grassIsland.name = "GrassIslandTop";
            grassIsland.transform.SetParent(worldRoot.transform);
            grassIsland.transform.position = new Vector3(0f, -0.15f, 0.6f);
            grassIsland.transform.localScale = new Vector3(24.0f, 0.16f, 20.4f);
            ApplyColor(grassIsland, new Color(0.55f, 0.76f, 0.28f));
            dayNight.grassTerrainRenderer = grassIsland.GetComponent<Renderer>();

            // Cobblestone Market Plaza (East side matching @Mnilax frames)
            GameObject plaza = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plaza.name = "CobblestoneMarketPlaza";
            plaza.transform.SetParent(worldRoot.transform);
            plaza.transform.position = new Vector3(6.6f, 0.01f, 2.2f);
            plaza.transform.localScale = new Vector3(6.4f, 0.04f, 6.2f);
            ApplyColor(plaza, new Color(0.68f, 0.64f, 0.59f));

            // 4. Hero Landmarks from Tripo 3D Models
            // Red Barn (Northwest)
            InstantiateTripoModelOrFallback(
                "barn", "RedFarmBarn",
                new Vector3(-3.8f, 0f, 6.4f), new Vector3(0f, 180f, 0f),
                5.4f, new Color(0.76f, 0.22f, 0.20f)
            ).transform.SetParent(worldRoot.transform);

            // Stone & Timber Windmill (North-East of Barn) + Rotating Sails
            GameObject windmill = InstantiateTripoModelOrFallback(
                "windmill", "StoneWindmill",
                new Vector3(3.4f, 0f, 6.2f), new Vector3(0f, 195f, 0f),
                6.6f, new Color(0.88f, 0.82f, 0.72f)
            );
            windmill.transform.SetParent(worldRoot.transform);
            BuildWindmillSpinningRotor(windmill.transform);

            // 3 Red-and-White Striped Market Stalls along the Eastern Cobblestone Plaza
            Vector3[] stallPositions =
            {
                new Vector3(5.6f, 0f, 4.3f),
                new Vector3(8.0f, 0f, 3.6f),
                new Vector3(9.8f, 0f, 2.8f)
            };
            for (int i = 0; i < stallPositions.Length; i++)
            {
                InstantiateTripoModelOrFallback(
                    "market_stall", $"MarketStall_{i + 1}",
                    stallPositions[i], new Vector3(0f, 192f, 0f),
                    3.1f, new Color(0.86f, 0.25f, 0.22f)
                ).transform.SetParent(worldRoot.transform);
            }

            // Arched Wooden Bridge (Far East over coastal water channel, frame_14)
            InstantiateTripoModelOrFallback(
                "wooden_bridge", "ArchedWoodenBridge",
                new Vector3(9.4f, -0.05f, -0.4f), new Vector3(0f, 25f, 0f),
                2.1f, new Color(0.54f, 0.34f, 0.18f)
            ).transform.SetParent(worldRoot.transform);

            // Broadleaf & Apple Trees framing North & South (matching frame_01 & frame_14)
            Vector3[] treePositions =
            {
                new Vector3(-0.3f, 0f, 7.2f),
                new Vector3(1.1f, 0f, 6.6f),
                new Vector3(5.4f, 0f, 6.8f),
                new Vector3(-3.6f, 0f, -5.8f),
                new Vector3(-1.4f, 0f, -6.1f),
                new Vector3(3.8f, 0f, -6.0f)
            };
            for (int i = 0; i < treePositions.Length; i++)
            {
                InstantiateTripoModelOrFallback(
                    "apple_tree", $"IslandTree_{i + 1}",
                    treePositions[i], new Vector3(0f, i * 57f, 0f),
                    4.1f, new Color(0.42f, 0.68f, 0.24f)
                ).transform.SetParent(worldRoot.transform);
            }

            // Shepherd's-Crook Street Lanterns with Warm Point Lights
            Vector3[] lanternPositions =
            {
                new Vector3(3.1f, 0f, 3.4f),
                new Vector3(3.6f, 0f, 1.8f),
                new Vector3(4.5f, 0f, 0.2f),
                new Vector3(6.6f, 0f, 4.4f),
                new Vector3(8.8f, 0f, 3.8f)
            };
            for (int i = 0; i < lanternPositions.Length; i++)
            {
                GameObject lantern = InstantiateTripoModelOrFallback(
                    "street_lantern", $"StreetLantern_{i + 1}",
                    lanternPositions[i], new Vector3(0f, 180f, 0f),
                    2.2f, new Color(0.28f, 0.24f, 0.22f)
                );
                lantern.transform.SetParent(worldRoot.transform);

                GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.name = "WarmLanternBulb";
                bulb.transform.SetParent(lantern.transform);
                bulb.transform.position = lanternPositions[i] + new Vector3(0f, 1.75f, 0f);
                bulb.transform.localScale = Vector3.one * 0.22f;
                ApplyColor(bulb, new Color(1.0f, 0.85f, 0.42f));
                dayNight.lanternBulbRenderers.Add(bulb.GetComponent<Renderer>());

                GameObject ptLightObj = new GameObject("LanternPointLight");
                ptLightObj.transform.SetParent(lantern.transform);
                ptLightObj.transform.position = lanternPositions[i] + new Vector3(0f, 1.85f, 0f);
                Light pt = ptLightObj.AddComponent<Light>();
                pt.type = LightType.Point;
                pt.color = new Color(1.0f, 0.72f, 0.32f);
                pt.range = 6.5f;
                pt.intensity = 1.6f;
                dayNight.streetLanternLights.Add(pt);
            }

            // 5. North Plot (Pre-populated with Mature Pumpkins & Tomato Bushes matching frame_01)
            BuildNorthShowcasePlot(worldRoot.transform, plotGrid);

            // 6. Central Interactive 7-Furrow Tilled Plot + Wooden Perimeter Fence
            BuildCentralInteractivePlot(worldRoot.transform, plotGrid);

            // 7. Tripo Farmer Character + Watering Can + Droplet Spray
            BuildFarmerPlayer(camObj.transform);
        }

        private static void BuildWindmillSpinningRotor(Transform windmillTransform)
        {
            GameObject rotorHub = new GameObject("WindmillSpinningRotor");
            rotorHub.transform.SetParent(windmillTransform);
            rotorHub.transform.position = windmillTransform.position + new Vector3(0f, 4.6f, -1.15f);
            WindmillRotor rotor = rotorHub.AddComponent<WindmillRotor>();
            rotor.rotationAxis = new Vector3(0f, 0f, 1f);
            rotor.degreesPerSecond = 38f;

            for (int i = 0; i < 4; i++)
            {
                GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.name = $"RotorSail_{i + 1}";
                blade.transform.SetParent(rotorHub.transform);
                float angle = i * 90f + 35f;
                blade.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                blade.transform.localPosition = Quaternion.Euler(0f, 0f, angle) * new Vector3(0f, 1.45f, 0f);
                blade.transform.localScale = new Vector3(0.42f, 2.6f, 0.08f);
                ApplyColor(blade, new Color(0.68f, 0.45f, 0.24f));
            }
        }

        private static void BuildNorthShowcasePlot(Transform parent, FarmPlotGrid plotGrid)
        {
            GameObject northPlot = new GameObject("NorthShowcasePlot");
            northPlot.transform.SetParent(parent);

            GameObject soilBed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            soilBed.name = "NorthSoilBed";
            soilBed.transform.SetParent(northPlot.transform);
            soilBed.transform.position = new Vector3(-1.2f, 0.04f, 3.0f);
            soilBed.transform.localScale = new Vector3(7.4f, 0.10f, 3.0f);
            ApplyColor(soilBed, new Color(0.36f, 0.21f, 0.12f));

            // 7 columns x 2 rows of lush Tomato Vines (back) + 2 rows of plump Orange Pumpkins (front)
            for (int col = 0; col < 7; col++)
            {
                float x = -3.9f + col * 0.9f;
                for (int r = 0; r < 2; r++)
                {
                    float zTom = 3.55f + r * 0.58f;
                    InstantiateTripoModelOrFallback(
                        "crop_tomato", $"NorthTomato_{col}_{r}",
                        new Vector3(x, 0.08f, zTom), new Vector3(0f, (col * 37f) % 360f, 0f),
                        0.78f, new Color(0.28f, 0.65f, 0.22f)
                    ).transform.SetParent(northPlot.transform);

                    float zPmp = 2.05f + r * 0.62f;
                    InstantiateTripoModelOrFallback(
                        "crop_pumpkin", $"NorthPumpkin_{col}_{r}",
                        new Vector3(x, 0.08f, zPmp), new Vector3(0f, (col * 53f) % 360f, 0f),
                        0.52f, new Color(0.95f, 0.48f, 0.12f)
                    ).transform.SetParent(northPlot.transform);
                }
            }
        }

        private static void BuildCentralInteractivePlot(Transform parent, FarmPlotGrid plotGrid)
        {
            GameObject centralRoot = new GameObject("CentralInteractive7FurrowPlot");
            centralRoot.transform.SetParent(parent);

            // Base dark soil pad
            GameObject basePad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePad.name = "CentralSoilBase";
            basePad.transform.SetParent(centralRoot.transform);
            basePad.transform.position = new Vector3(-0.6f, 0.03f, -1.6f);
            basePad.transform.localScale = new Vector3(8.2f, 0.08f, 5.4f);
            ApplyColor(basePad, new Color(0.33f, 0.19f, 0.10f));

            // 7 Raised Furrow Strips x 6 Interactive Crop Slots
            for (int col = 0; col < 7; col++)
            {
                float x = -3.6f + col * 1.0f;

                GameObject furrowStrip = GameObject.CreatePrimitive(PrimitiveType.Cube);
                furrowStrip.name = $"FurrowStrip_{col + 1}";
                furrowStrip.transform.SetParent(centralRoot.transform);
                furrowStrip.transform.position = new Vector3(x, 0.08f, -1.6f);
                furrowStrip.transform.localScale = new Vector3(0.68f, 0.10f, 4.9f);
                ApplyColor(furrowStrip, new Color(0.42f, 0.25f, 0.14f));

                for (int row = 0; row < 6; row++)
                {
                    float z = 0.45f - row * 0.82f;
                    GameObject slotObj = new GameObject($"CropSlot_C{col}_R{row}");
                    slotObj.transform.SetParent(centralRoot.transform);
                    slotObj.transform.position = new Vector3(x, 0.12f, z);

                    GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    mound.name = "SoilMound";
                    mound.transform.SetParent(slotObj.transform);
                    mound.transform.localPosition = new Vector3(0f, -0.02f, 0f);
                    mound.transform.localScale = new Vector3(0.62f, 0.04f, 0.62f);
                    ApplyColor(mound, new Color(0.42f, 0.25f, 0.14f));

                    // 4-Leaf Green Sprout Visual (matching @Mnilax frame_05..frame_14)
                    GameObject sprout = BuildFourLeafSprout(slotObj.transform);
                    GameObject bush = BuildGrowingBush(slotObj.transform);

                    GameObject pumpkinVis = InstantiateTripoModelOrFallback(
                        "crop_pumpkin", "MaturePumpkin",
                        slotObj.transform.position, Vector3.zero,
                        0.52f, new Color(0.95f, 0.48f, 0.12f)
                    );
                    pumpkinVis.transform.SetParent(slotObj.transform);

                    GameObject tomatoVis = InstantiateTripoModelOrFallback(
                        "crop_tomato", "MatureTomato",
                        slotObj.transform.position, Vector3.zero,
                        0.76f, new Color(0.28f, 0.65f, 0.22f)
                    );
                    tomatoVis.transform.SetParent(slotObj.transform);

                    CropSlot slot = slotObj.AddComponent<CropSlot>();
                    slot.rowIndex = row;
                    slot.colIndex = col;
                    slot.InitializeVisuals(
                        mound.GetComponent<Renderer>(),
                        sprout,
                        bush,
                        pumpkinVis,
                        tomatoVis
                    );

                    plotGrid.interactiveSlots.Add(slot);
                }
            }

            // Wooden Post-and-Rail Perimeter Fence around the plot
            BuildWoodenFenceBorder(centralRoot.transform, new Vector3(-0.6f, 0f, -1.6f), 9.0f, 6.2f);
        }

        private static GameObject BuildFourLeafSprout(Transform parent)
        {
            GameObject root = new GameObject("FourLeafSprout");
            root.transform.SetParent(parent);
            root.transform.localPosition = Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                leaf.name = $"Leaf_{i}";
                leaf.transform.SetParent(root.transform);
                float ang = i * 90f + 45f;
                leaf.transform.localRotation = Quaternion.Euler(25f, ang, 0f);
                leaf.transform.localPosition = Quaternion.Euler(0f, ang, 0f) * new Vector3(0f, 0.08f, 0.12f);
                leaf.transform.localScale = new Vector3(0.14f, 0.05f, 0.24f);
                ApplyColor(leaf, new Color(0.32f, 0.78f, 0.22f));
            }
            return root;
        }

        private static GameObject BuildGrowingBush(Transform parent)
        {
            GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.name = "GrowingBush";
            bush.transform.SetParent(parent);
            bush.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            bush.transform.localScale = new Vector3(0.45f, 0.38f, 0.45f);
            ApplyColor(bush, new Color(0.26f, 0.68f, 0.20f));
            return bush;
        }

        private static void BuildWoodenFenceBorder(Transform parent, Vector3 center, float width, float depth)
        {
            GameObject fenceRoot = new GameObject("WoodenPerimeterFence");
            fenceRoot.transform.SetParent(parent);
            Color woodColor = new Color(0.62f, 0.39f, 0.21f);

            int xPosts = 8;
            for (int i = 0; i <= xPosts; i++)
            {
                float x = center.x - width * 0.5f + (width / xPosts) * i;
                float zSouth = center.z - depth * 0.5f;
                CreateFencePost(fenceRoot.transform, new Vector3(x, 0f, zSouth), woodColor);
            }
            CreateFenceRail(fenceRoot.transform, new Vector3(center.x, 0.34f, center.z - depth * 0.5f), new Vector3(width, 0.07f, 0.07f), woodColor);
            CreateFenceRail(fenceRoot.transform, new Vector3(center.x, 0.18f, center.z - depth * 0.5f), new Vector3(width, 0.07f, 0.07f), woodColor);

            int zPosts = 6;
            for (int i = 0; i <= zPosts; i++)
            {
                float z = center.z - depth * 0.5f + (depth / zPosts) * i;
                float xWest = center.x - width * 0.5f;
                CreateFencePost(fenceRoot.transform, new Vector3(xWest, 0f, z), woodColor);
            }
            CreateFenceRail(fenceRoot.transform, new Vector3(center.x - width * 0.5f, 0.34f, center.z), new Vector3(0.07f, 0.07f, depth), woodColor);
            CreateFenceRail(fenceRoot.transform, new Vector3(center.x - width * 0.5f, 0.18f, center.z), new Vector3(0.07f, 0.07f, depth), woodColor);
        }

        private static void CreateFencePost(Transform parent, Vector3 pos, Color c)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = "FencePost";
            p.transform.SetParent(parent);
            p.transform.position = pos + new Vector3(0f, 0.24f, 0f);
            p.transform.localScale = new Vector3(0.12f, 0.48f, 0.12f);
            ApplyColor(p, c);
        }

        private static void CreateFenceRail(Transform parent, Vector3 pos, Vector3 scale, Color c)
        {
            GameObject r = GameObject.CreatePrimitive(PrimitiveType.Cube);
            r.name = "FenceRail";
            r.transform.SetParent(parent);
            r.transform.position = pos;
            r.transform.localScale = scale;
            ApplyColor(r, c);
        }

        private static void BuildFarmerPlayer(Transform camTransform)
        {
            GameObject farmerRoot = new GameObject("FarmerPlayer");
            farmerRoot.transform.position = new Vector3(-0.6f, 0f, 0.45f);
            farmerRoot.transform.rotation = Quaternion.Euler(0f, 135f, 0f);

            GameObject visualHolder = new GameObject("VisualRoot");
            visualHolder.transform.SetParent(farmerRoot.transform);
            visualHolder.transform.localPosition = Vector3.zero;

            GameObject tripoFarmer = InstantiateTripoModelOrFallback(
                "farmer", "TripoFarmerMesh",
                farmerRoot.transform.position, farmerRoot.transform.eulerAngles,
                1.55f, new Color(0.25f, 0.48f, 0.78f)
            );
            tripoFarmer.transform.SetParent(visualHolder.transform);

            GameObject wateringCan = InstantiateTripoModelOrFallback(
                "watering_can", "EquippedWateringCan",
                farmerRoot.transform.position + new Vector3(0.32f, 0.48f, 0.34f),
                farmerRoot.transform.eulerAngles,
                0.42f, new Color(0.45f, 0.62f, 0.78f)
            );
            wateringCan.transform.SetParent(visualHolder.transform);

            FarmerController ctrl = farmerRoot.AddComponent<FarmerController>();
            ctrl.cameraRig = camTransform;
            ctrl.visualRoot = visualHolder.transform;
            ctrl.wateringCanTool = wateringCan;
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };
        }

        private static void BuildStandaloneMacApp()
        {
            string buildPath = "Builds/CozyIslandFarm.app";
            Directory.CreateDirectory("Builds");
            BuildPlayerOptions opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = buildPath,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"[STANDALONE BUILD] Result={report.summary.result}, Output={buildPath}");
        }
    }
}
#endif
