﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using KKAPI;
using KKAPI.Studio.SaveLoad;
using Studio;

namespace ObjImport
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInDependency(ExtensibleSaveFormat.ExtendedSave.GUID, ExtensibleSaveFormat.ExtendedSave.Version)]
    [BepInProcess("CharaStudio")]
    public class ObjImport : BaseUnityPlugin
    {
        public string path = "";

        public const string PluginName = "KK_ObjImport";
        public const string GUID = "org.njaecha.plugins.objimport";
        public const string Version = "1.1.0";

        internal new static ManualLogSource Logger;

        private bool uiActive = false;
        private ConfigEntry<KeyboardShortcut> hotkey;
        private Rect windowRect = new Rect(500, 40, 240, 140);
        private int scaleSelection = 0;
        private string[] scaleGridText = { "1", "0.5", "1.5", "2", "0.1", "0.01", "0.001", "0.0001" };
        private float[] scales = { 1f, 0.5f, 1f, 2f, 0.1f, 0.01f, 0.001f, 0.0001f };


        public static List<ObjectCtrlInfo> remeshedObjects = new List<ObjectCtrlInfo>();

        void Awake()
        {
            ObjImport.Logger = base.Logger;
            KeyboardShortcut defaultShortcut = new KeyboardShortcut(KeyCode.O);
            hotkey = Config.Bind("General", "Hotkey", defaultShortcut, "Press this key to open UI");
            StudioSaveLoadApi.RegisterExtraBehaviour<SceneController>(GUID);
        }

        void Update()
        {
            if (hotkey.Value.IsDown())
                uiActive = !uiActive;
        }
        public void LoadMesh()
        {
            if (!File.Exists(path))
            {
                Logger.LogWarning($"File [{path}] does not exist");
                return;
            }
            else
            {
                IEnumerable<ObjectCtrlInfo> selectedObjects = KKAPI.Studio.StudioAPI.GetSelectedObjects();
                Mesh mesh = new Mesh();
                Boolean runImport = false;
                List<ObjectCtrlInfo> selectItems = new List<ObjectCtrlInfo>();

                foreach (ObjectCtrlInfo oci in selectedObjects)
                {
                    if (oci is OCIItem)
                    {
                        OCIItem item = (OCIItem)oci;
                        if (item.objectItem.GetComponentInChildren<MeshFilter>())
                        {                            
                            runImport = true;
                            selectItems.Add(oci);
                        }
                        else Logger.LogWarning($"No MeshFilter found on selected Item [{item.objectItem.name}]");
                    }
                    else Logger.LogWarning("Selected object is not an Item");

                }

                if (runImport)
                {
                    if (path.EndsWith(".obj") || path.EndsWith(".OBJ"))
                    {
                        mesh = meshFromObj(path);
                        Logger.LogInfo($"Loaded mesh from file [{path}]");
                        //Logger.LogInfo($"Vertex count obj: {mesh.vertexCount}");
                        //Logger.LogInfo($"Triangle count obj: {mesh.triangles.Length}");
                        if (mesh == null)
                            return;
                        foreach (var i in selectItems)
                        {
                            remeshObject(i, mesh);
                            OCIItem item = (OCIItem)i;
                            Logger.LogInfo($"Mesh applied to object [{item.objectItem.name}]");
                            i.treeNodeObject.textName = path.Substring(path.LastIndexOf("/")).Remove(0, 4);
                        }
                    }
                    else
                    {
                        Logger.LogWarning($"File [{path}] is not an OBJ file");

                    }

                }
            }
        }
        private Mesh meshFromObj(string path)
        {
            Mesh mesh = new Mesh();
            string[] lines = File.ReadAllLines(path);
            int vertexCount = 0;

            foreach (string line in lines)
            {
                if (line.StartsWith("f "))
                {
                    vertexCount++;
                }
            }
            vertexCount = vertexCount * 3;

            mesh = new ObjImporter().ImportFile(path, (vertexCount > 65000));
            if (mesh == null)
            {
                Logger.LogError("Mesh too big, not supported in this version of Unity.");
            }
            else if (scaleSelection != 0)
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
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
            }
            return mesh;
        }

        public static void remeshObject(ObjectCtrlInfo oci, Mesh mesh)
        {
            OCIItem item = (OCIItem)oci;
            item.objectItem.GetComponentInChildren<MeshFilter>().mesh = mesh;
            remeshedObjects.Add(oci);
        }
          
        void OnGUI()
        {
            if (uiActive)
            {
                windowRect = GUI.Window(345, windowRect, WindowFunction, "Obj Import");
                KKAPI.Utilities.IMGUIUtils.EatInputInRect(windowRect);
            }
        }
        private void WindowFunction(int WindowID)
        {
            path = GUI.TextField(new Rect(10, 20, 220, 20), path);
            scaleSelection = GUI.SelectionGrid(new Rect(10, 50, 220, 40), scaleSelection, scaleGridText, 4);
            if (GUI.Button(new Rect(10, 100, 220, 30), "Import OBJ"))
            {
                LoadMesh();
            }
            GUI.DragWindow();
        }
    }
}
