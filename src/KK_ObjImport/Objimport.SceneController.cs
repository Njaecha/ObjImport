using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using KKAPI;
using Studio;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using ExtensibleSaveFormat;
using MessagePack;
using KK_Plugins.MaterialEditor;

namespace ObjImport
{
    class SceneController: SceneCustomFunctionController
    {
        protected override void OnSceneSave()
        {
            PluginData data = new PluginData();

            List<int> IDs = new List<int>();
            List<List<Mesh>> meshes = new List<List<Mesh>>();

            Dictionary<int, ObjectCtrlInfo> idObjectPairs = Studio.Studio.Instance.dicObjectCtrl;
            foreach (int id in idObjectPairs.Keys)
            {
                if (ObjImport.sceneRemeshedObjects.ContainsKey(idObjectPairs[id]))
                {
                    ObjImport.Logger.LogDebug($"Saving meshes for ID [{id}] | {idObjectPairs[id].guideObject.name}");
                    IDs.Add(id);
                    meshes.Add(ObjImport.sceneRemeshedObjects[idObjectPairs[id]]);
                }
            }
            if (meshes.Count > 0)
            {
                data.data.Add("version", 1);
                List<byte[]> byteArrays = new List<byte[]>();
                foreach(List<Mesh> objectMeshes in meshes)
                {
                    byteArrays.Add(SimpleMeshSerializer.Serialize(objectMeshes));
                }
                data.data.Add("meshes", MessagePackSerializer.Serialize(byteArrays));
                data.data.Add("ids", MessagePackSerializer.Serialize(IDs));
            }

            SetExtendedData(data);
        }

        protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            var data = GetExtendedData();

            if (operation == SceneOperationKind.Clear || operation == SceneOperationKind.Load)
            {
                ObjImport.sceneRemeshedObjects.Clear();
            }

            if (data == null) return;
            if (operation == SceneOperationKind.Clear) return;

            int version = 0;
            if (data.data.TryGetValue("version", out var versionS) && versionS != null)
            {
                version = (int)versionS;
            }
            if (version == 0) //backwards compatibility
            {
                List<int> IDs = new List<int>();
                List<Mesh> meshes = new List<Mesh>();


                if (data.data.TryGetValue("meshes", out var meshesSerialized) && meshesSerialized != null)
                {
                    meshes = (List<Mesh>)SimpleMeshSerializer.Deserialize((byte[])meshesSerialized);
                }
                if (data.data.TryGetValue("ids", out var ids) && ids != null)
                {
                    IDs = MessagePackSerializer.Deserialize<List<int>>((byte[])ids);
                }
                if (IDs.Count > 0)
                    for (int x = 0; x < IDs.Count; x++)
                    {
                        OCIItem item = (OCIItem)loadedItems[IDs[x]];
                        Mesh mesh = meshes[x];
                        ObjImport.remeshObject(item, new List<Mesh> { mesh });
                        ObjImport.Logger.LogDebug($"Mesh loaded for (old) ID [{IDs[x]}]:  {item.objectItem.name}");
                    }
            }
            else if (version == 1) 
            {
                List<int> IDs = new List<int>();
                List<List<Mesh>> meshes = new List<List<Mesh>>();

                if (data.data.TryGetValue("meshes", out var meshesSerialized) && meshesSerialized != null)
                {
                    List<byte[]> byteArrays = MessagePackSerializer.Deserialize<List<byte[]>>((byte[])meshesSerialized);
                    foreach(byte[] byteArray in byteArrays)
                    {
                        meshes.Add((List<Mesh>)SimpleMeshSerializer.Deserialize(byteArray));
                    }
                }
                if (data.data.TryGetValue("ids", out var ids) && ids != null)
                {
                    IDs = MessagePackSerializer.Deserialize<List<int>>((byte[])ids);
                }
                if (IDs.Count > 0)
                    for (int x = 0; x < IDs.Count; x++)
                    {
                        OCIItem item = (OCIItem)loadedItems[IDs[x]];
                        List<Mesh> objectMeshes = meshes[x];
                        ObjImport.remeshObject(item, objectMeshes);
                        ObjImport.Logger.LogDebug($"Meshes loaded for (old) ID [{IDs[x]}]:  {item.objectItem.name}");
                    }
            }
            
        }
        protected override void OnObjectDeleted(ObjectCtrlInfo oci)
        {
            if(oci is OCIItem)
            {
                if (ObjImport.sceneRemeshedObjects.Keys.Contains(oci))
                {
                    ObjImport.sceneRemeshedObjects.Remove(oci);
                }
            }
        }
        protected override void OnObjectsCopied(ReadOnlyDictionary<Int32, ObjectCtrlInfo> copiedItems)
        {
            Dictionary<int, ObjectCtrlInfo> sceneObjects = Studio.Studio.Instance.dicObjectCtrl;
            foreach (int id in copiedItems.Keys)
            {
                if (copiedItems[id] is OCIItem)
                {
                    OCIItem newItem = (OCIItem)copiedItems[id];
                    OCIItem oldItem = (OCIItem)sceneObjects[id];
                    if (ObjImport.sceneRemeshedObjects.ContainsKey(sceneObjects[id]))
                    {
                        ObjImport.remeshObject(copiedItems[id], ObjImport.sceneRemeshedObjects[oldItem]);
                        newItem.treeNodeObject.textName = oldItem.treeNodeObject.textName;
                        ObjImport.Logger.LogDebug($"Meshes copied from {oldItem.objectItem.name} to {newItem.objectItem.name}");
                    }
                }
            }
        }
    }
}
