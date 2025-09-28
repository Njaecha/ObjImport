using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAPI;
using KKAPI.Chara;
using KKAPI.Studio.SaveLoad;
using Studio;
using HarmonyLib;
using Vectrosity;
using System.CodeDom;

namespace ObjImport
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInDependency(ExtensibleSaveFormat.ExtendedSave.GUID, ExtensibleSaveFormat.ExtendedSave.Version)]
    [BepInDependency(KK_Plugins.MaterialEditor.MaterialEditorPlugin.PluginGUID, KK_Plugins.MaterialEditor.MaterialEditorPlugin.PluginVersion)]
    public class ObjImport : BaseUnityPlugin
    {
        //plugin
        public const string PluginName = "KKS_ObjImport";
        public const string GUID = "org.njaecha.plugins.objimport";
        public const string Version = "3.1.1";

        internal new static ManualLogSource Logger;

        //Importer
        public ObjImporter importer = null;
        public ObjImporterAdvanced importerAdvanced = null;
        public static MaterialImporter materialImporter = null;
        public static string selectedShaderKey = "Standard";
        public static List<string> availableShaderSelection = new List<string> { 
            "Standard", 
            "KKUSSitem",
            "KKUTSitem",
            "AIT/Item",
            "Shader Forge/main_item_ditherd",
            "Shader Forge/main_color",
            "xukmi/MainItemPlus"
        };

        //ui
        public string path = "";
        private bool uiActive = false;
        private ConfigEntry<KeyboardShortcut> hotkey;
        private ConfigEntry<string> defaultDir;
        private ConfigEntry<bool> debugUIElements;
        private Rect windowRect = new Rect(500, 40, 240, 170);
        private int scaleSelection = 4;
        private float[] scales = { 10f, 5f, 2f, 1.5f, 1.0f, 0.5f, 0.1f, 0.01f, 0.001f, 0.0001f };
        private bool displayHelp = false;
        private bool displayAdvanced = false;
        private bool flipX = true;
        private bool flipY = false;
        private bool flipZ = false;
        private bool multiObjectMode = true;

        //studio
        public static Dictionary<ObjectCtrlInfo, List<Mesh>> sceneRemeshedObjects = new Dictionary<ObjectCtrlInfo, List<Mesh>>();
        bool drawDebug = false;
        List<VectorLine> debugLines = new List<VectorLine>();

        public static Dictionary<string, string> meshNameToMeshMaterial = new Dictionary<string, string>();

        void Awake()
        {
            ObjImport.Logger = base.Logger;
            ObjImport.materialImporter = new MaterialImporter(base.Logger);
            //config
            KeyboardShortcut defaultShortcut = new KeyboardShortcut(KeyCode.O);
            hotkey = Config.Bind("_General_", "Hotkey", defaultShortcut, "Press this key to open the UI");
            defaultDir = Config.Bind("_General_", "Default Directory", "C:", "The default directory of the file dialoge.");
            debugUIElements = Config.Bind("Debug", "DebugUI", false, "Draw Debug UI Elements");
            //Extra Behaviours
            StudioSaveLoadApi.RegisterExtraBehaviour<SceneController>(GUID);
            CharacterApi.RegisterExtraBehaviour<CharacterController>(GUID);
            //Harmony
            Harmony harmony = Harmony.CreateAndPatchAll(typeof(Hooks), null);
            //CharacterController events
            KKAPI.Maker.AccessoriesApi.AccessoryKindChanged += AccessoryKindChanged;
            KKAPI.Maker.AccessoriesApi.AccessoriesCopied += AccessoryCopied;
            KKAPI.Maker.AccessoriesApi.AccessoryTransferred += AccessoryTransferred;
        }

        private void AccessoryTransferred(object sender, KKAPI.Maker.AccessoryTransferEventArgs e)
        {
            int dSlot = e.DestinationSlotIndex;
            int sSlot = e.SourceSlotIndex;
            KKAPI.Maker.MakerAPI.GetCharacterControl().gameObject.GetComponent<CharacterController>().accessoryTransferedEvent(sSlot, dSlot);
        }

        private void AccessoryCopied(object sender, KKAPI.Maker.AccessoryCopyEventArgs e)
        {
            ChaFileDefine.CoordinateType dType = e.CopyDestination;
            ChaFileDefine.CoordinateType sType = e.CopySource;
            IEnumerable<int> slots = e.CopiedSlotIndexes;
            KKAPI.Maker.MakerAPI.GetCharacterControl().gameObject.GetComponent<CharacterController>().accessoryCopiedEvent((int)sType, (int)dType, slots);
        }

        private void AccessoryKindChanged(object sender, KKAPI.Maker.AccessorySlotEventArgs e)
        {
            int slot = e.SlotIndex;
            KKAPI.Maker.MakerAPI.GetCharacterControl().gameObject.GetComponent<CharacterController>().accessoryChangeEvent(slot);
        }

        void Update()
        {
            if (hotkey.Value.IsDown() && (KKAPI.KoikatuAPI.GetCurrentGameMode() == GameMode.Maker || KKAPI.KoikatuAPI.GetCurrentGameMode() == GameMode.Studio))
                uiActive = !uiActive;
            if (!debugUIElements.Value) return;
            if (drawDebug) drawDebugLines();
        }

        /// <summary>
        /// Load button method
        /// </summary>
        public void LoadMesh()
        {
            if (path == "")
            {
                Logger.LogMessage("Please choose a .obj file");
                return;
            }
            path = path.Replace("\"", "");
            path = path.Replace("\\","/");
            if (!File.Exists(path))
            {
                Logger.LogMessage($"File [{path}] does not exist");
                return;
            }
            else
            {
                if (KoikatuAPI.GetCurrentGameMode() == GameMode.Studio)
                {
                    IEnumerable<ObjectCtrlInfo> selectedObjects = KKAPI.Studio.StudioAPI.GetSelectedObjects();
                    Mesh mesh = new Mesh();
                    List<ObjectCtrlInfo> selectItems = new List<ObjectCtrlInfo>();

                    foreach (ObjectCtrlInfo oci in selectedObjects)
                    {
                        if (oci is OCIItem)
                        {
                            OCIItem item = (OCIItem)oci;
                            if (item.objectItem.GetComponentInChildren<MeshFilter>())
                            {
                                selectItems.Add(oci);
                            }
                            else Logger.LogWarning($"No MeshFilter found on selected Item [{item.objectItem.name}]");
                        }
                    }

                    if (path.EndsWith(".obj") || path.EndsWith(".OBJ"))
                    {
                        if (multiObjectMode == false)
                        {
                            mesh = meshWithMaterialFromObj(path);
                            if (mesh == null)
                                return;
                            Logger.LogInfo($"Loaded mesh from file [{path}]");
                            if (selectItems.Count >= 1)
                            {
                                foreach (var i in selectItems)
                                {
                                    remeshObject(i, new List<Mesh> { mesh });
                                    OCIItem item = (OCIItem)i;
                                    Logger.LogInfo($"Mesh applied to object [{item.objectItem.name}]");
                                    i.treeNodeObject.textName = path.Substring(path.LastIndexOf("/")).Remove(0, 1);
                                }
                            }
                            else
                            {
                                OCIItem item = Studio.AddObjectItem.Add(1, 1, 1);
                                remeshObject(item, new List<Mesh> { mesh });
                                Logger.LogInfo($"Mesh applied to object [{item.objectItem.name}]");
                                item.treeNodeObject.textName = path.Substring(path.LastIndexOf("/")).Remove(0, 1);
                            }
                        }
                        else
                        {
                            List<Mesh> meshes = meshesWithMaterialFromObj(path);
                            if (meshes == null)
                                return;
                            Logger.LogMessage($"Successfully loaded meshes from [{path}]");
                            
                            if (selectItems.Count >= 1)
                            {
                                foreach (var i in selectItems)
                                {
                                    remeshObject(i, meshes);
                                    Logger.LogInfo($"Mesh applied to object [{((OCIItem)i).objectItem.name}]");
                                    ((OCIItem)i).treeNodeObject.textName = path.Substring(path.LastIndexOf("/")).Remove(0, 1);
                                }
                            }
                            else
                            {
                                OCIItem item = Studio.AddObjectItem.Add(1, 1, 1);
                                remeshObject(item, meshes);
                                Logger.LogInfo($"Mesh applied to object [{item.objectItem.name}]");
                                item.treeNodeObject.textName = path.Substring(path.LastIndexOf("/")).Remove(0, 1);
                            }
                        }
                    }
                    else
                    {
                        Logger.LogWarning($"File [{path}] is not an OBJ file");

                    }
                }
                else if (KoikatuAPI.GetCurrentGameMode() == GameMode.Maker)
                {
                    ChaAccessoryComponent ac = KKAPI.Maker.MakerAPI.GetCharacterControl().GetAccessoryComponent(KKAPI.Maker.AccessoriesApi.SelectedMakerAccSlot);
                    if (ac == null)
                    {
                        Logger.LogMessage("Current slot has no accessory that could be replaced");
                        return;
                    }
                    if (path.EndsWith(".obj") || path.EndsWith(".OBJ"))
                    {
                        if (multiObjectMode == false)
                        {
                            Mesh mesh = meshWithMaterialFromObj(path);
                            if (mesh == null)
                            {
                                Logger.LogMessage("Loading failed!");
                                return;
                            }
                            Logger.LogMessage($"Successfully loaded meshes from [{path}]");
                            if (remeshObject(KKAPI.Maker.MakerAPI.GetCharacterControl(), KKAPI.Maker.MakerAPI.GetCharacterControl().fileStatus.coordinateType, KKAPI.Maker.AccessoriesApi.SelectedMakerAccSlot, ac, new List<Mesh> { mesh }))
                            {
                                Logger.LogInfo($"Replaced accessory in slot {KKAPI.Maker.AccessoriesApi.SelectedMakerAccSlot} with {path.Substring(path.LastIndexOf("/")).Remove(0, 1)}");
                            }
                        }
                        else
                        {
                            List<Mesh> meshes = meshesWithMaterialFromObj(path);
                            if (meshes == null)
                            {
                                Logger.LogMessage("Loading failed!");
                                return;
                            }
                            Logger.LogMessage($"Successfully loaded meshes from [{path}]");
                            if (remeshObject(KKAPI.Maker.MakerAPI.GetCharacterControl(), KKAPI.Maker.MakerAPI.GetCharacterControl().fileStatus.coordinateType, KKAPI.Maker.AccessoriesApi.SelectedMakerAccSlot, ac, meshes))
                            {
                                Logger.LogInfo($"Replaced accessory in slot {KKAPI.Maker.AccessoriesApi.SelectedMakerAccSlot} with {path.Substring(path.LastIndexOf("/")).Remove(0, 1)}");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Loads a mesh and fills textures in the material importer.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private Mesh meshWithMaterialFromObj(string path)
        {
            Logger.LogMessage("Loading Mesh+Material...");
            importer = new ObjImporter(Logger);

            // count faces to see if >65535
            int vertexCount = 0;
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.StartsWith("f "))
                {
                    string[] parts = line.Split(' ');
                    vertexCount += (parts.Length - 1);
                }
            }

            //Logger.LogMessage("Start MeshData Import...");
            MeshDto meshData = importer.ImportFile(path);

            //Logger.LogMessage("Converting to Meshes...");
            Mesh mesh = importer.convertToMesh(meshData, (vertexCount > 65535));

            //Override the mesh name to resemble singular file (will also apply to material mapping)
            string objFileName = path.Substring(path.LastIndexOf("/"));
            mesh.name = objFileName.Remove(objFileName.LastIndexOf(".")).Remove(0, 1);
            meshData.name = mesh.name;

            //Register the mapping from mesh to meshDto (remember connection)
            //Logger.LogMessage("Mapping: " + mesh.name + " - " + meshData.usedMaterial);
            meshNameToMeshMaterial[mesh.name] = meshData.usedMaterial;

            //Populate the material importer memory
            Logger.LogMessage("Start Material Import...");
            ObjImport.materialImporter.importMaterials(path, meshData);

            if (mesh == null)
                Logger.LogError("Mesh+Material could not be loaded.");
            else
            {
                Vector3[] baseVertices = mesh.vertices;
                var vertices = new Vector3[baseVertices.Length];
                for (var i = 0; i < vertices.Length; i++)
                {
                    var vertex = baseVertices[i];
                    vertex.x = (float)(vertex.x * scales[scaleSelection]);
                    vertex.y = (float)(vertex.y * scales[scaleSelection]);
                    vertex.z = (float)(vertex.z * scales[scaleSelection]);
                    vertices[i] = vertex;
                }
                mesh.vertices = vertices;
                flipCoordinates(mesh);

            }

            return mesh;
        }

        /// <summary>
        /// Loads a mesh and fills textures in the material importer.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private List<Mesh> meshesWithMaterialFromObj(string path)
        {
            Logger.LogMessage("Loading Meshes+Material...");

            importerAdvanced = new ObjImporterAdvanced(Logger);
            ObjImport.materialImporter = new MaterialImporter(Logger);

            List<Mesh> meshes = new List<Mesh>();
            string[] lines = File.ReadAllLines(path);
            int vertexCount = 0;

            foreach (string line in lines)
            {
                if (line.StartsWith("f "))
                {
                    char[] splitIdentifier = { ' ' };
                    string[] x = line.Split(splitIdentifier);
                    vertexCount += (x.Length - 1);
                }
            }

            //Logger.LogMessage("Start Advanced MeshData Import...");
            List<MeshAdvancedDto> meshDatas = importerAdvanced.ImportFile(path);

            //Logger.LogMessage("Converting to Meshes...");
            meshes = importerAdvanced.convertToMesh(meshDatas, (vertexCount > 65535));

            //Register the mapping from mesh to meshDto (remember connection)
            for (int i = 0; i < meshes.Count; i++)
            {
                meshNameToMeshMaterial[meshes[i].name] = meshDatas[i].usedMaterial;
            }

            //Populate the material importer memory
            Logger.LogMessage("Start Material Import...");
            ObjImport.materialImporter.importMaterials(path, meshDatas);

            if (meshes == null)
                Logger.LogError("Mesh could not be loaded.");
            else
            {
                foreach (Mesh mesh in meshes)
                {
                    Vector3[] baseVertices = mesh.vertices;
                    var vertices = new Vector3[baseVertices.Length];
                    for (var i = 0; i < vertices.Length; i++)
                    {
                        var vertex = baseVertices[i];
                        vertex.x = (float)(vertex.x * scales[scaleSelection]);
                        vertex.y = (float)(vertex.y * scales[scaleSelection]);
                        vertex.z = (float)(vertex.z * scales[scaleSelection]);
                        vertices[i] = vertex;
                    }
                    mesh.vertices = vertices;
                    flipCoordinates(mesh);
                }
            }
            return meshes;
        }

        /// <summary>
        /// Flips a mesh according to the current flip settings
        /// </summary>
        /// <param name="mesh">Mesh to flip</param>
        public void flipCoordinates(Mesh mesh)
        {
            Vector3[] baseVertices = mesh.vertices;
            Vector3[] baseNormals = mesh.normals;
            var vertices = new Vector3[baseVertices.Length];
            var normals = new Vector3[baseNormals.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                var vertex = baseVertices[i];
                var normal = baseNormals[i];
                if (flipX) { vertex.x = -vertex.x; normal.x = -normal.x; }
                if (flipY) { vertex.y = -vertex.y; normal.y = -normal.y; }
                if (flipZ) { vertex.z = -vertex.z; normal.z = -normal.z; }
                vertices[i] = vertex;
                normals[i] = normal;
            }
            mesh.vertices = vertices;
            mesh.normals = normals;
            int flips = 0;
            if (flipX) flips++;
            if (flipY) flips++;
            if (flipZ) flips++;
            if (flips == 1 || flips == 3)   //inverse triangle order if a 1 or all axes got flipped
            {
                int[] baseTriangles = mesh.triangles;
                int[] triangles = new int[baseTriangles.Length];
                for (int x = 0; x < baseTriangles.Length; x += 3)
                {
                    triangles[x] = baseTriangles[x];
                    triangles[x + 1] = baseTriangles[x + 2];
                    triangles[x + 2] = baseTriangles[x + 1];
                }
                mesh.triangles = triangles;
            }
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// Creates a multi object studioItem on basis of the passed object
        /// </summary>
        /// <param name="oci">ObjectCtrlInfo of the item. Should only have one MeshFilter and MeshRenderer</param>
        /// <param name="meshes">List of Meshes to apply, can be only one</param>
        internal static void remeshObject(ObjectCtrlInfo oci, List<Mesh> meshes)
        {
            GameObject rootObject = ((OCIItem)oci).objectItem;

            ItemComponent itemComponent = rootObject.GetComponent<ItemComponent>();
            if (itemComponent == null)
            {
                Logger.LogMessage("ERROR: studioItem has not ItemComponent!");
                return;
            }

            //Destory all subobjects of the studioItem that render a mesh, except the first.
            for (int x = 1; x < ((OCIItem)oci).arrayRender.Length; x++)
            {
                var renderer = ((OCIItem)oci).arrayRender[x];
                DestroyImmediate(renderer.gameObject);
            }

            MeshFilter meshFilter = rootObject.GetComponentInChildren<MeshFilter>();
            MeshRenderer meshRenderer = rootObject.GetComponentInChildren<MeshRenderer>();
            GameObject first = meshFilter.transform.gameObject;

            List<Renderer> renderes = new List<Renderer>();
            meshFilter.mesh = meshes[0];


            if (meshes[0].name != null && meshes[0].name != "")
            {
                first.name = meshes[0].name;
                meshRenderer.material.name = meshes[0].name;


                //string materialKey = ((OCIItem)oci).treeNodeObject.textName;
                string materialKey = "default";
                if (meshNameToMeshMaterial.ContainsKey(meshes[0].name))
                {
                    materialKey = meshNameToMeshMaterial[meshes[0].name];
                }

                //look up loaded material texture
                if (ObjImport.materialImporter.meshMaterialMap.ContainsKey(materialKey))
                {
                    MtlData mtlData = ObjImport.materialImporter.meshMaterialMap[materialKey];
                    RegisterMaterialsWithMaterialEditor(meshes[0], meshRenderer, mtlData);
                }
                else
                {
                    Logger.LogMessage("No Material of Key: " + materialKey);
                }

            }

            renderes.Add(meshRenderer);


            for (int i = 1; i < meshes.Count; i++)
            {
                Mesh mesh = meshes[i];
                GameObject addObject = new GameObject(mesh.name);
                addObject.layer = 10;
                addObject.transform.SetParent(rootObject.transform);
                addObject.transform.position = meshFilter.transform.position;
                addObject.transform.rotation = meshFilter.transform.rotation;
                addObject.transform.localScale = meshFilter.transform.localScale;
                MeshFilter addMeshFilter = addObject.AddComponent<MeshFilter>();

                //string materialKey = ((OCIItem)oci).treeNodeObject.textName;
                string materialKey = "default";
                if (meshNameToMeshMaterial.ContainsKey(mesh.name))
                {
                    materialKey = meshNameToMeshMaterial[mesh.name];
                }

                //look up loaded material texture
                if (ObjImport.materialImporter.meshMaterialMap.ContainsKey(materialKey))
                {
                    MtlData mtlData = ObjImport.materialImporter.meshMaterialMap[materialKey];
                    Renderer newRenderer = RegisterMaterialsWithMaterialEditor(addObject, mesh, meshRenderer, mtlData);
                    renderes.Add(newRenderer);
                }
                else
                {
                    Logger.LogMessage("No Material of Key: " + materialKey);
                    MeshRenderer addMeshRenderer = addObject.AddComponent<MeshRenderer>();
                    Material secondMaterial = new Material(meshRenderer.material);
                    secondMaterial.name = mesh.name;
                    addMeshRenderer.material = secondMaterial;
                    renderes.Add(addMeshRenderer);
                }

                addMeshFilter.mesh = mesh;

            }
            Renderer[] newRendererArray = renderes.ToArray();

            ((OCIItem)oci).arrayRender = newRendererArray;
            itemComponent.rendNormal = newRendererArray;
            itemComponent.rendAlpha = new Renderer[0];

            KK_Plugins.MaterialEditor.MaterialEditorPlugin.ClearCache(rootObject);
            sceneRemeshedObjects[oci] = meshes;
        }

        /// <summary>
        /// Applies a new mesh to an accessory
        /// </summary>
        /// <param name="character">Character that "owns" the accessory</param>
        /// <param name="outfitType">Index (type) of the coordinate that the accessory is part of</param>
        /// <param name="slot">Slot of the accessory</param>
        /// <param name="accessoryComponent">AccessoryComponent of the accessory</param>
        /// <param name="meshes">Meshes to apply</param>
        /// <returns></returns>
        public static bool remeshObject(ChaControl character, int outfitType, int slot, ChaAccessoryComponent accessoryComponent, List<Mesh> meshes)
        {
            GameObject rootObject = accessoryComponent.gameObject;
            MeshFilter meshFilter = rootObject.GetComponentInChildren<MeshFilter>();
            CharacterController controller = character.gameObject.GetComponent<CharacterController>();
            if (meshFilter != null)
            {
                GameObject first = meshFilter.transform.gameObject;
                MeshRenderer meshRenderer = rootObject.GetComponentInChildren<MeshRenderer>();

                List<Renderer> renderes = new List<Renderer>();

                //Destory parts of the accessory with MeshFilters until only one is left
                while (rootObject.GetComponentsInChildren<MeshFilter>().Length > 1)
                {
                    DestroyImmediate(rootObject.GetComponentsInChildren<MeshFilter>()[1].gameObject);
                }
                meshFilter.mesh = meshes[0];
                if (meshes[0].name != "")
                {
                    first.name = meshes[0].name;
                    meshRenderer.material.name = meshes[0].name;

                    string materialKey = "default";
                    if (meshNameToMeshMaterial.ContainsKey(meshes[0].name))
                    {
                        materialKey = meshNameToMeshMaterial[meshes[0].name];
                    }

                    //look up loaded material texture
                    if (ObjImport.materialImporter.meshMaterialMap.ContainsKey(materialKey))
                    {
                        MtlData mtlData = ObjImport.materialImporter.meshMaterialMap[materialKey];
                        RegisterMaterialsWithMaterialEditor(meshes[0], meshRenderer, mtlData);
                    }
                    else
                    {
                        Logger.LogMessage("No Material of Key: " + materialKey);
                    }

                }

                renderes.Add(meshRenderer);

                for (int i = 1; i < meshes.Count; i++)
                {
                    Mesh mesh = meshes[i];
                    GameObject addObject = new GameObject(mesh.name);
                    addObject.layer = 10;
                    addObject.transform.SetParent(first.transform.parent);
                    addObject.transform.position = meshFilter.transform.position;
                    addObject.transform.rotation = meshFilter.transform.rotation;
                    addObject.transform.localScale = meshFilter.transform.localScale;
                    MeshFilter addMeshFilter = addObject.AddComponent<MeshFilter>();

                    string materialKey = "default";
                    if (meshNameToMeshMaterial.ContainsKey(mesh.name))
                    {
                        materialKey = meshNameToMeshMaterial[mesh.name];
                    }

                    //look up loaded material texture
                    if (ObjImport.materialImporter.meshMaterialMap.ContainsKey(materialKey))
                    {
                        MtlData mtlData = ObjImport.materialImporter.meshMaterialMap[materialKey];
                        Renderer newRenderer = RegisterMaterialsWithMaterialEditor(addObject, mesh, meshRenderer, mtlData);
                        renderes.Add(newRenderer);
                    }
                    else
                    {
                        Logger.LogMessage("No Material of Key: " + materialKey);
                        MeshRenderer addMeshRenderer = addObject.AddComponent<MeshRenderer>();
                        Material secondMaterial = new Material(meshRenderer.material);
                        secondMaterial.name = mesh.name;
                        addMeshRenderer.material = secondMaterial;
                        renderes.Add(addMeshRenderer);
                    }

                    addMeshFilter.mesh = mesh;
                }

                Renderer[] newRendNormalArray = renderes.ToArray();

                accessoryComponent.rendNormal = newRendNormalArray;
                if (!controller.remeshData.ContainsKey(outfitType))
                    controller.remeshData[outfitType] = new Dictionary<int, List<Mesh>>();
                if (!(controller.remeshData[outfitType].ContainsKey(slot) && controller.remeshData[outfitType][slot] == meshes))
                    controller.remeshData[outfitType][slot] = meshes;

                KK_Plugins.MaterialEditor.MaterialEditorPlugin.ClearCache(rootObject);
                return true;
            }
            else
            {
                Logger.LogMessage("Accessory seems to be dynamic, please select a static one to replace");
                return false;
            }
        }


        internal static void RegisterMaterialsWithMaterialEditor(Mesh mesh, Renderer renderer, MtlData mtlData)
        {
            /*
            renderer.material.name = mtlData.name;
            renderer.material.mainTexture = mtlData.texture;
            renderer.material.color = mtlData.diffuseColor;
            */

            ObjImport.materialImporter.FillMaterial(renderer.material, mtlData);

        }

        internal static Renderer RegisterMaterialsWithMaterialEditor(GameObject gameObj, Mesh mesh, Renderer renderer, MtlData mtlData)
        {
            MeshRenderer addMeshRenderer = gameObj.AddComponent<MeshRenderer>();

            Material secondMaterial = new Material(renderer.material);
            ObjImport.materialImporter.FillMaterial(secondMaterial, mtlData);

            addMeshRenderer.material = secondMaterial;

            return addMeshRenderer;
        }

        void OnGUI()
        {
            if (uiActive && (KKAPI.KoikatuAPI.GetCurrentGameMode() == GameMode.Maker || KKAPI.KoikatuAPI.GetCurrentGameMode() == GameMode.Studio))
            {
                windowRect = GUI.Window(345, windowRect, WindowFunction, "Obj Import v"+ Version);
                KKAPI.Utilities.IMGUIUtils.EatInputInRect(windowRect);
            }
            if (!debugUIElements.Value) return;
            if (GUI.Button(new Rect(150,150,200,40), "Draw Normal for Select"))
            {
                drawDebug = !drawDebug;
                if (drawDebug)
                {
                    OCIItem item = (OCIItem)KKAPI.Studio.StudioAPI.GetSelectedObjects().First();
                    setDebugLines(item);
                }
                else
                {
                    VectorLine.Destroy(debugLines);
                    debugLines.Clear();
                }
            }
        }

        private bool isDropdownOpen = false; // Track if the dropdown menu is open

        private void WindowFunction(int WindowID)
        {
            if (KKAPI.KoikatuAPI.GetCurrentGameMode() == GameMode.MainGame) return;

            // Shader Keys (Retrieve keys from the dictionary)
            List<string> shaderKeys = KK_Plugins.MaterialEditor.MaterialEditorPlugin.LoadedShaders.Keys.ToList();
            int selectedShaderIndex = shaderKeys.IndexOf(ObjImport.selectedShaderKey);

            if (selectedShaderIndex < 0)
            {
                foreach (string shaderKey in shaderKeys)
                {
                    selectedShaderIndex = shaderKeys.IndexOf(ObjImport.selectedShaderKey);
                    if (selectedShaderIndex >= 0)
                    {
                        break;
                    }
                }
            }

            // Dropdown for selecting shader
            GUI.Label(new Rect(10, 20, 220, 20), "Select Shader:");

            // Button that opens the dropdown
            if (GUI.Button(new Rect(10, 45, 220, 20), selectedShaderIndex >= 0 ? shaderKeys[selectedShaderIndex] : "No matching shader found!"))
            {
                isDropdownOpen = !isDropdownOpen; // Toggle the dropdown visibility
            }

            // If the dropdown is open, display the options
            if (isDropdownOpen)
            {
                windowRect.width = 240 + 250;
                GUI.Box(new Rect(245, 20, 240, windowRect.height - 30), "Shader Selection Menu");

                for (int i = 0; i < ObjImport.availableShaderSelection.Count; i++)
                {
                    int tempShaderIndex = shaderKeys.IndexOf(ObjImport.availableShaderSelection[i]);

                    if (tempShaderIndex >= 0)
                    {
                        // Display the options as buttons
                        if (GUI.Button(new Rect(255, 45 + (i * 20), 220, 20), ObjImport.availableShaderSelection[i]))
                        {
                            ObjImport.selectedShaderKey = ObjImport.availableShaderSelection[i];
                            isDropdownOpen = false; // Close the dropdown after selection
                            break; // Break after selecting to avoid multiple selections
                        }
                    }
                    else
                    {
                        // instead of Button, show info that there would be more options
                        if (GUI.Button(new Rect(255, 45 + (i * 20), 220, 20), ObjImport.availableShaderSelection[i] + " (Unavailable)"))
                        {
                            //Change nothing, since unavailable

                            isDropdownOpen = false; // Close the dropdown after selection
                            break; // Break after selecting to avoid multiple selections
                        }
                    }
                }
            }
            else
            {
                windowRect.width = 240;
            }

            if (KKAPI.KoikatuAPI.GetCurrentGameMode() == GameMode.MainGame) return;
            path = GUI.TextField(new Rect(10, 70, 195, 20), path);
            if (GUI.Button(new Rect(205, 70, 25, 20), "..."))
            {
                path = path.Replace("\\","/");
                string dir = (path == "") ? defaultDir.Value : path.Replace(path.Substring(path.LastIndexOf("/")), "");
                //Logger.LogInfo(dir);
                KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags SingleFileFlags = 
                    KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags.OFN_FILEMUSTEXIST | 
                    KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES | 
                    KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER;
                string[] file = KKAPI.Utilities.OpenFileDialog.ShowDialog("Open OBJ file", dir, "OBJ files (*.obj)|*.obj", "obj", SingleFileFlags);
                if (file != null)
                {
                    path = file[0];
                }
            }
            if (GUI.Button(new Rect(10, 95, 220, 25), multiObjectMode ? "☑️ Multi-Object Mode": "☐ Multi-Object Mode"))
            {
                multiObjectMode = !multiObjectMode;
            }

            GUI.Label(new Rect(10, 125, 160, 25), $"Scaling-factor: {scales[scaleSelection]}");
            if (scaleSelection == 0) GUI.enabled = false;
            if (GUI.Button(new Rect(190, 125,20,20), "+"))
            {
                scaleSelection--;
            }
            GUI.enabled = true;
            if (scaleSelection == 9) GUI.enabled = false;
            if (GUI.Button(new Rect(210, 125, 20, 20), "-"))
            {
                scaleSelection++;
            }
            GUI.enabled = true;

            if (GUI.Button(new Rect(10, 150, 220, 30), "Import OBJ"))
            {
                LoadMesh();
            }
            if (GUI.Button(new Rect(10, 185, 110, 20), "Help"))
            {
                displayHelp = !displayHelp;
                displayAdvanced = false;
            }
            if (GUI.Button(new Rect(120, 185, 110, 20), "Advanced"))
            {
                displayHelp = false;
                displayAdvanced = !displayAdvanced;
            }
            if (displayHelp)
            {
                windowRect.height = 355;
                string helpText = "";
                if (KKAPI.KoikatuAPI.GetCurrentGameMode() == GameMode.Studio)
                    helpText = "If you have an studioItem selected, it will be replaced." +
                        "\nYou can change the scale of the object with the scaling-factor." +
                        "\nMulti-Object Mode gives you the ability to apply different material per object." +
                        "\nIf you get weird lighting and/or textures, try the mirror feature in [advanced].";
                else if (KKAPI.KoikatuAPI.GetCurrentGameMode() == GameMode.Maker)
                    helpText = "On Import, the currently selected accessory will be replaced." +
                        "\nYou can change the scale of the object with the scaling-factor." +
                        "\nMulti-Object Mode gives you the ability to apply different material per object." +
                        "\nIf you get weird lighting and/or textures, try the mirror feature in [advanced].";
                GUI.Label(new Rect(10, 205, 220, 150), helpText);
            }
            if (displayAdvanced)
            {
                windowRect.height = 295;
                GUI.Label(new Rect(10, 205, 220, 25), "Mirror along axis:");
                flipX = GUI.Toggle(new Rect(10, 230, 70, 20), flipX, " X-Axis");
                flipY = GUI.Toggle(new Rect(10, 250, 70, 20), flipY, " Y-Axis");
                flipZ = GUI.Toggle(new Rect(10, 270, 70, 20), flipZ, " Z-Axis");
            }
            if (!displayHelp && !displayAdvanced) windowRect.height = 215;
            GUI.DragWindow();
        }

        private void multiObjectDevTest()
        {
            Logger.LogInfo("test");
            ChaControl chara = KKAPI.Maker.MakerAPI.GetCharacterControl();
            chara.ChangeAccessory(KKAPI.Maker.AccessoriesApi.SelectedMakerAccSlot, 122, 14, "");

        }

        private static void printRelevantMeshInfo( Mesh mesh)
        {
            string line = "Mesh Info:";
            line += $"\nName: {mesh.name}";
            line += $"\nVertices: {mesh.vertices.Length}";
            line += $"\nUV: {mesh.uv.Length}";
            line += $"\nNormals: {mesh.normals.Length}";
            line += $"\nTriangles: {mesh.triangles.Length / 3}";
            Logger.LogInfo(line);
        }
        private void setDebugLines(OCIItem item)
        {
            Logger.LogInfo("DEBUG: set debug lines");
            foreach (MeshFilter filter in item.objectItem.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.mesh;
                List<Vector3> points = new List<Vector3>();
                for (int i = 0; i < mesh.vertices.Length; i++)
                {
                    points.Add(mesh.vertices[i] + item.objectItem.transform.position);
                    points.Add(mesh.vertices[i] + mesh.normals[i] + item.objectItem.transform.position);
                }
                VectorLine normalLine = new VectorLine($"{mesh.GetHashCode()}", points, 2); 
                normalLine.lineType = LineType.Discrete;
                debugLines.Add(normalLine);
            }

        }
        private void drawDebugLines()
        {
            if (debugLines.Count > 0)
            {
                foreach(VectorLine line in debugLines)
                {
                    line.Draw();
                }
            }
        }
    }
}
