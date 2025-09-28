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
    [MessagePackObject] // Serializable version of MtlData
    public class SerializableMtlData
    {
        [Key(0)] public string name;
        [Key(1)] public string diffuseTexPath;
        [Key(2)] public Color diffuseColor = Color.white; 
        [Key(3)] public byte[] textureData; // Raw texture data instead of Texture2D
        [Key(4)] public Color ambientColor = Color.black; // Ka
        [Key(5)] public Color specularColor = Color.black; // Ks
        [Key(6)] public Color emissionColor = Color.black; // Ke
        [Key(7)] public float shininess = 0f; // Ns
        [Key(8)] public float refractionIndex = 1.0f; // Ni
        [Key(9)] public float transparency = 1.0f; // d
        [Key(10)] public int illuminationModel = 2; // illum
        [Key(11)] public string shaderKey = "Standard";


        // Default constructor for MessagePack
        public SerializableMtlData() { }

        // Constructor to convert MtlData to SerializableMtlData
        public SerializableMtlData(MtlData data)
        {
            name = data.name;
            diffuseTexPath = data.diffuseTexPath;
            diffuseColor = data.diffuseColor;
            ambientColor = data.ambientColor;
            specularColor = data.specularColor;
            emissionColor = data.emissionColor;
            shininess = data.shininess;
            refractionIndex = data.refractionIndex;
            transparency = data.transparency;
            illuminationModel = data.illuminationModel;
            shaderKey = data.shaderKey;

            if (data.texture != null)
            {
                try
                {
                    textureData = data.texture.EncodeToPNG(); // Encode texture as PNG
                }
                catch (Exception e)
                {
                    ObjImport.Logger.LogError($"Failed to encode texture for material {data.name}: {e}");
                    textureData = null;
                }
            }
        }

        // Convert back to MtlData
        public MtlData ToMtlData()
        {
            Texture2D tex = null;
            if (textureData != null && textureData.Length > 0)
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(textureData); // Load the image data into a Texture2D
            }

            return new MtlData
            {
                name = this.name,
                diffuseTexPath = this.diffuseTexPath,
                diffuseColor = this.diffuseColor,
                ambientColor = this.ambientColor,
                specularColor = this.specularColor,
                emissionColor = this.emissionColor,
                shininess = this.shininess,
                refractionIndex = this.refractionIndex,
                transparency = this.transparency,
                illuminationModel = this.illuminationModel,
                shaderKey = this.shaderKey,
                texture = tex
            };
        }
    }

    class SceneController : SceneCustomFunctionController
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
                data.data.Add("version", 2);

                // Save meshes
                List<byte[]> byteArrays = new List<byte[]>();
                foreach (List<Mesh> objectMeshes in meshes)
                {
                    byteArrays.Add(SimpleMeshSerializer.Serialize(objectMeshes));
                }
                data.data.Add("meshes", MessagePackSerializer.Serialize(byteArrays));
                data.data.Add("ids", MessagePackSerializer.Serialize(IDs));

                // Save materials
                var serializableMaterials = new Dictionary<string, SerializableMtlData>();
                foreach (var kvp in ObjImport.materialImporter.meshMaterialMap)
                {
                    serializableMaterials[kvp.Key] = new SerializableMtlData(kvp.Value);
                }
                data.data.Add("materials", MessagePackSerializer.Serialize(serializableMaterials));


                data.data.Add("meshMaterialLookup", MessagePackSerializer.Serialize(ObjImport.meshNameToMeshMaterial));

            }

            SetExtendedData(data);
        }

        protected override void OnSceneLoad(SceneOperationKind operation, ReadOnlyDictionary<int, ObjectCtrlInfo> loadedItems)
        {

            var data = GetExtendedData();

            //Resets the mateialImporter on scene load
            ObjImport.materialImporter = new MaterialImporter(ObjImport.Logger);

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
                    foreach (byte[] byteArray in byteArrays)
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
            else if (version == 2)
            {
                List<int> IDs = new List<int>();
                List<List<Mesh>> meshes = new List<List<Mesh>>();

                // Load meshes
                if (data.data.TryGetValue("meshes", out var meshesSerialized) && meshesSerialized != null)
                {
                    List<byte[]> byteArrays = MessagePackSerializer.Deserialize<List<byte[]>>((byte[])meshesSerialized);
                    foreach (byte[] byteArray in byteArrays)
                    {
                        meshes.Add((List<Mesh>)SimpleMeshSerializer.Deserialize(byteArray));
                    }
                }

                if (data.data.TryGetValue("ids", out var ids) && ids != null)
                {
                    IDs = MessagePackSerializer.Deserialize<List<int>>((byte[])ids);
                }

                // Load materials
                if (data.data.TryGetValue("materials", out var materialsSerialized) && materialsSerialized != null)
                {
                    var serializableMaterials =
                        MessagePackSerializer.Deserialize<Dictionary<string, SerializableMtlData>>((byte[])materialsSerialized);

                    foreach (var kvp in serializableMaterials)
                    {
                        ObjImport.materialImporter.meshMaterialMap[kvp.Key] = kvp.Value.ToMtlData();
                    }
                    ObjImport.Logger.LogMessage($"Restored {serializableMaterials.Count} materials from scene file.");
                }

                // Load mesh-to-material map
                if (data.data.TryGetValue("meshMaterialLookup", out var mapSerialized) && mapSerialized != null)
                {
                    ObjImport.meshNameToMeshMaterial =
                        MessagePackSerializer.Deserialize<Dictionary<string, string>>((byte[])mapSerialized);

                    //ObjImport.Logger.LogMessage($"Restored mesh-to-material map with {ObjImport.meshNameToMeshMaterial.Count} entries.");
                }

                // Reapply meshes
                if (IDs.Count > 0)
                {
                    for (int x = 0; x < IDs.Count; x++)
                    {
                        OCIItem item = (OCIItem)loadedItems[IDs[x]];
                        List<Mesh> objectMeshes = meshes[x];
                        ObjImport.remeshObject(item, objectMeshes);
                        //ObjImport.Logger.LogMessage($"Meshes + materials loaded for ID [{IDs[x]}]: {item.objectItem.name}");
                    }
                }
            }
        }
        protected override void OnObjectDeleted(ObjectCtrlInfo oci)
        {
            if (oci is OCIItem)
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
                        newItem.treeNodeObject.textName = oldItem.treeNodeObject.textName;
                        ObjImport.remeshObject(copiedItems[id], ObjImport.sceneRemeshedObjects[oldItem]);
                        //ObjImport.Logger.LogDebug($"Meshes copied from {oldItem.objectItem.name} to {newItem.objectItem.name}");
                    }
                }
            }
        }
    }
}
