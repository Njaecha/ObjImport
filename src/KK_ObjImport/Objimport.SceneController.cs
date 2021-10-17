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

namespace ObjImport
{
    class SceneController: SceneCustomFunctionController
    {
        protected override void OnSceneSave()
        {
            PluginData data = new PluginData();

            List<int> IDs = new List<int>();
            List<Mesh> meshes = new List<Mesh>();

            Dictionary<int, ObjectCtrlInfo> idObjectPairs = Studio.Studio.Instance.dicObjectCtrl;
            foreach (int id in idObjectPairs.Keys)
            {
                if (ObjImport.remeshedObjects.Contains(idObjectPairs[id]))
                {
                    ObjImport.Logger.LogDebug($"Mesh saved for ID: [{id}] | {idObjectPairs[id].guideObject.name}");
                    OCIItem item = (OCIItem)idObjectPairs[id];
                    IDs.Add(id);
                    meshes.Add(item.objectItem.GetComponentInChildren<MeshFilter>().mesh);
                }
            }
            if (meshes.Count > 0)
            {
                data.data.Add("meshes", SimpleMeshSerializer.Serialize(meshes));
                data.data.Add("ids", MessagePackSerializer.Serialize(IDs));
            }
            else
            {
                data.data.Add("meshes", null);
                data.data.Add("ids", null);
            }

            SetExtendedData(data);
        }

        protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {
            var data = GetExtendedData();

            if (operation == SceneOperationKind.Clear || operation == SceneOperationKind.Load)
            {
                ObjImport.remeshedObjects.Clear();
            }

            if (data == null) return;
            if (operation == SceneOperationKind.Clear) return;

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
                    ObjImport.remeshObject(item, mesh);
                    ObjImport.Logger.LogDebug($"Mesh loaded for (old) ID [{IDs[x]}]:  {item.objectItem.name}");
                }
        }
        protected override void OnObjectDeleted(ObjectCtrlInfo oci)
        {
            if(oci is OCIItem)
            {
                if (ObjImport.remeshedObjects.Contains(oci))
                {
                    ObjImport.remeshedObjects.Remove(oci);
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
                    if (ObjImport.remeshedObjects.Contains(sceneObjects[id]))
                    {
                        ObjImport.remeshObject(copiedItems[id], oldItem.objectItem.GetComponentInChildren<MeshFilter>().mesh);
                        newItem.treeNodeObject.textName = oldItem.treeNodeObject.textName;
                        ObjImport.Logger.LogDebug($"Mesh copied from {oldItem.objectItem.name} to {newItem.objectItem.name}");
                    }
                }
            }
        }
    }
}
