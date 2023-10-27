using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using KKAPI;
using KKAPI.Chara;
using ExtensibleSaveFormat;
using MessagePack;
using MaterialEditorAPI;
using KK_Plugins.MaterialEditor;

namespace ObjImport
{
    class CharacterController : CharaCustomFunctionController
    {
        public Dictionary<int, Dictionary<int, List<Mesh>>> remeshData = new Dictionary<int, Dictionary<int, List<Mesh>>>();

        /// <summary>
        /// Fills the PluginData with data required for a Coordinate Card
        /// </summary>
        /// <param name="data">PluginData to be filed</param>
        /// <returns></returns>
        private PluginData fillCoordinateData(PluginData data)
        {
            ChaFileCoordinate coordinate = ChaControl.nowCoordinate;
            List<int> slots = new List<int>();
            List<List<Mesh>> meshes = new List<List<Mesh>>();
            for (int i = 0; i < coordinate.accessory.parts.Length; i++)
            {
                if (remeshData[ChaControl.fileStatus.coordinateType].Keys.Contains(i))
                {
                    slots.Add(i);
                    meshes.Add(remeshData[ChaControl.fileStatus.coordinateType][i]);
                }
            }
            data.data.Add("slots", MessagePackSerializer.Serialize(slots));
            List<byte[]> byteArrays = new List<byte[]>();
            foreach (List<Mesh> objectMeshes in meshes)
            {
                byteArrays.Add(SimpleMeshSerializer.Serialize(objectMeshes));
            }
            data.data.Add("meshes", MessagePackSerializer.Serialize(byteArrays));

            if (slots.Count > 0)
            {
                string savingLog = $"Saved custom meshes for:";
                for (int x = 0; x < slots.Count; x++)
                {
                    savingLog += $"\nSlot {slots[x] + 1}";
                }
                ObjImport.Logger.LogDebug(savingLog);
            }
            return data;
        }

        /// <summary>
        /// Fills the PluginData with data required for a Character Card
        /// </summary>
        /// <param name="data">PluginData to be filled</param>
        /// <returns></returns>
        private PluginData fillCharacterData(PluginData data)
        {
            ChaFileCoordinate[] coordinates = ChaControl.chaFile.coordinate;
            int coordinateNum = 0;
            for (int y = 0; y < coordinates.Length; y++)
            {
                ChaFileCoordinate coordinate = coordinates[y];
                List<int> slots = new List<int>();
                List<List<Mesh>> meshes = new List<List<Mesh>>();
                for (int i = 0; i < coordinate.accessory.parts.Length; i++)
                {
                    if (remeshData.Keys.Contains(y))
                    {
                        if (remeshData[y].Keys.Contains(i))
                        {
                            slots.Add(i);
                            meshes.Add(remeshData[y][i]);
                        }
                    }
                }
                data.data.Add($"slots{coordinateNum}", MessagePackSerializer.Serialize(slots));
                List<byte[]> byteArrays = new List<byte[]>();
                foreach (List<Mesh> objectMeshes in meshes)
                {
                    byteArrays.Add(SimpleMeshSerializer.Serialize(objectMeshes));
                }
                data.data.Add($"meshes{coordinateNum}", MessagePackSerializer.Serialize(byteArrays));

                if (slots.Count > 0)
                {
                    string savingLog = $"Saved custom meshes on coordinate with index {coordinateNum} for:";
                    for (int x = 0; x < slots.Count; x++)
                    {
                        savingLog += $"\nSlot {slots[x]}";
                    }
                    ObjImport.Logger.LogDebug(savingLog);
                }
                coordinateNum++;
            }

            return data;
        }

        /// <summary>
        /// Applies the data saved in PluginData to a Coordinate
        /// </summary>
        /// <param name="data">PluginData to be loaded</param>
        /// <param name="coordinate">Coordinate to apply on</param>
        /// <returns></returns>
        private bool applyCoordinateData(PluginData data, ChaFileCoordinate coordinate)
        {
            if (data == null) return false;

            bool didSomething = false;

            List<int> slots = new List<int>();
            List<List<Mesh>> meshes = new List<List<Mesh>>();

            if (data.data.TryGetValue("meshes", out var meshesSerialized) && meshesSerialized != null)
            {
                List<byte[]> byteArrays = MessagePackSerializer.Deserialize<List<byte[]>>((byte[])meshesSerialized);
                foreach (byte[] byteArray in byteArrays)
                {
                    meshes.Add((List<Mesh>)SimpleMeshSerializer.Deserialize(byteArray));
                }
            }
            if (data.data.TryGetValue("slots", out var slotsSerialized) && slotsSerialized != null)
            {
                slots = MessagePackSerializer.Deserialize<List<int>>((byte[])slotsSerialized);
            }

            bool noIssue = true;

            for(int i = 0; i < slots.Count; i++)
            {
                if (!ObjImport.remeshObject(ChaControl, ChaControl.fileStatus.coordinateType, slots[i], ChaControl.GetAccessoryComponent(slots[i]), meshes[i]))
                {
                    ObjImport.Logger.LogError($"Remeshing of accessory in slot {slots[i]} failed!");
                    noIssue = false;
                }
                didSomething = true;
            }
            if (didSomething) ChaControl.StartCoroutine(ChaControl.GetComponent<MaterialEditorCharaController>().LoadData(false, true, false));
            return noIssue;
        }

        /// <summary>
        /// Applies the data saved in PluginData to this Character
        /// </summary>
        /// <param name="data">PluginData to apply</param>
        /// <returns></returns>
        private bool applyCharacterData(PluginData data)
        {
            if (data == null) return false;

            bool noIssue = true;
            bool didSomething = false;

            Dictionary<int, List<int>> coSlots = new Dictionary<int, List<int>>();
            Dictionary<int, List<List<Mesh>>> coMeshes = new Dictionary<int, List<List<Mesh>>>();

            for (int x = 0; x < ChaControl.chaFile.coordinate.Length; x++)
            {
                ChaFileCoordinate coordinate = ChaControl.chaFile.coordinate[x];

                List<int> slots = new List<int>();
                List<List<Mesh>> meshes = new List<List<Mesh>>();

                if (data.data.TryGetValue($"meshes{x}", out var meshesSerialized) && meshesSerialized != null)
                {
                    List<byte[]> byteArrays = MessagePackSerializer.Deserialize<List<byte[]>>((byte[])meshesSerialized);
                    foreach (byte[] byteArray in byteArrays)
                    {
                        meshes.Add((List<Mesh>)SimpleMeshSerializer.Deserialize(byteArray));
                    }
                }
                if (data.data.TryGetValue($"slots{x}", out var slotsSerialized) && slotsSerialized != null)
                {
                    slots = MessagePackSerializer.Deserialize<List<int>>((byte[])slotsSerialized);
                }
                coSlots[x] = slots;
                coMeshes[x] = meshes;
                if (x == ChaControl.fileStatus.coordinateType && slots.Count > 0 && meshes.Count == slots.Count)
                {
                    for (int i = 0; i < slots.Count; i++)
                    {
                        if (!ObjImport.remeshObject(ChaControl, ChaControl.fileStatus.coordinateType, slots[i], ChaControl.GetAccessoryComponent(slots[i]), meshes[i]))
                        {
                            ObjImport.Logger.LogError($"Remeshing of accessory in slot {slots[i]} failed!");
                            noIssue = false;
                        }
                        didSomething = true;
                    }
                }
            }
            foreach (int type in coSlots.Keys)
            {
                if (!remeshData.Keys.Contains(type))
                {
                    remeshData[type] = new Dictionary<int, List<Mesh>>();
                    for(int z = 0; z < coSlots[type].Count; z++)
                    {
                        int slot = coSlots[type][z];
                        remeshData[type][slot] = coMeshes[type][z];
                    }
                }
            }
            if (didSomething) ChaControl.StartCoroutine(ChaControl.GetComponent<MaterialEditorCharaController>().LoadData(false, true, false));
            return noIssue;
        }

        /// <summary>
        /// Called whenever an accessory is Changed
        /// </summary>
        /// <param name="slot">slot of the changed accessory</param>
        internal void accessoryChangeEvent(int slot)
        {
            if (remeshData.ContainsKey(ChaControl.fileStatus.coordinateType))
                if (remeshData[ChaControl.fileStatus.coordinateType].ContainsKey(slot))
                {
                    remeshData[ChaControl.fileStatus.coordinateType].Remove(slot);
                }
        }
        /// <summary>
        /// Called whenever an accessory is Transfered
        /// </summary>
        /// <param name="source">Slot of the source</param>
        /// <param name="destination">Slot of the destination</param>
        internal void accessoryTransferedEvent(int source, int destination)
        {
            StartCoroutine(accessoryTransferedEventDelay(0.5f, source, destination));
        }
        /// <summary>
        /// Called whenever accessories get copied between coordinates
        /// </summary>
        /// <param name="source">Index (type) of the source coordinate</param>
        /// <param name="destination">Index (type) of the destination coordinate</param>
        /// <param name="slots">Slots of the copied accessories</param>
        internal void accessoryCopiedEvent(int source, int destination, IEnumerable<int> slots)
        {
            StartCoroutine(accessoryCopiedEventDelay(0.5f, source, destination, slots));
        }
        /// <summary>
        /// Called whenever the current outfit is chagned
        /// </summary>
        internal void coordintateChangeEvent()
        {
            StartCoroutine(coordinateChangeEventDelay(0.5f));
        }

        IEnumerator coordinateChangeEventDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            updateMeshes();
        }

        IEnumerator accessoryTransferedEventDelay(float delay, int source, int destination)
        {
            yield return new WaitForSeconds(delay);
            int type = ChaControl.fileStatus.coordinateType;
            if (remeshData.ContainsKey(type))
            {
                ObjImport.Logger.LogDebug("accessoryTransferedEvent");
                if (remeshData[type].ContainsKey(source))
                {
                    remeshData[type][destination] = remeshData[type][source];
                    ObjImport.Logger.LogDebug($"Source slot {source} --> Destination slot {destination}");
                }
                else if (remeshData[type].ContainsKey(destination))
                {
                    remeshData[type].Remove(destination);
                }
                updateMeshes();
            }
        }

        IEnumerator accessoryCopiedEventDelay(float delay, int source, int destination, IEnumerable<int> slots)
        {
            yield return new WaitForSeconds(delay);
            if (remeshData.ContainsKey(source))
            {
                if (!remeshData.ContainsKey(destination))
                    remeshData[destination] = new Dictionary<int, List<Mesh>>();
                foreach (int slot in slots)
                {
                    if (remeshData[source].ContainsKey(slot))
                    {
                        remeshData[destination][slot] = remeshData[source][slot];
                        ObjImport.Logger.LogDebug($"Source: Type {source}, Slot {slot} --> Destination: Type {destination}");
                    }
                    else if (remeshData[destination].ContainsKey(slot))
                    {
                        remeshData[destination].Remove(slot);
                    }
                }
                if (ChaControl.fileStatus.coordinateType == destination) ChaControl.Reload(false, true, true, true);
                
            }
            else if(remeshData.ContainsKey(destination))
            {
                foreach (int slot in slots)
                {
                    if (remeshData[destination].ContainsKey(slot))
                    {
                        remeshData[destination].Remove(slot);
                    }
                }
                if (ChaControl.fileStatus.coordinateType == destination) ChaControl.Reload(false, true, true, true);
            }
            if (ChaControl.fileStatus.coordinateType == source || ChaControl.fileStatus.coordinateType == destination)
                updateMeshes();
        }
        /// <summary>
        /// Updates all custom meshes on the current coordinate. Use when the game reloads accessories
        /// </summary>
        public void updateMeshes()
        {
            int type = ChaControl.fileStatus.coordinateType;
            if (remeshData.ContainsKey(type))
            {
                foreach (int slot in remeshData[type].Keys)
                {
                    ObjImport.remeshObject(ChaControl, type, slot, ChaControl.GetAccessoryComponent(slot), remeshData[type][slot]);
                }
                ChaControl.StartCoroutine(ChaControl.GetComponent<MaterialEditorCharaController>().LoadData(false, true, false));
            }
        }

        IEnumerator updateMeshesDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            updateMeshes();
        }
        IEnumerator applyDataDelayed(float delay, PluginData data, ChaFileCoordinate coordinate)
        {
            yield return new WaitForSeconds(delay);
            if (applyCoordinateData(data, coordinate))
            {
                //nothing right now
            }
        }
        IEnumerator applyCharacterDataDelayed(float delay, PluginData data)
        {
            yield return new WaitForSeconds(delay);
            if (applyCharacterData(data))
            {
                //nothing right now
            }
        }

        protected override void OnCardBeingSaved(GameMode currentGameMode)
        {
            if (!remeshData.IsNullOrEmpty() && remeshData.Where(item => !item.Value.IsNullOrEmpty()).Any())
            {
                PluginData data = new PluginData();
                SetExtendedData(fillCharacterData(data));
            }
        }

        protected override void OnReload(GameMode currentGameMode, Boolean maintainState)
        {
            if (KKAPI.KoikatuAPI.GetCurrentGameMode() == GameMode.Maker && !KKAPI.Maker.MakerAPI.InsideAndLoaded)
                return;
            if (KKAPI.KoikatuAPI.GetCurrentGameMode() == GameMode.Maker && !KKAPI.Maker.MakerAPI.GetCharacterLoadFlags().Clothes) 
                return;
            remeshData.Clear();
            PluginData data = GetExtendedData();
            StartCoroutine(applyCharacterDataDelayed(1f, data));
        }

        protected override void OnCoordinateBeingSaved(ChaFileCoordinate coordinate)
        {
            if (!remeshData.ContainsKey(ChaControl.fileStatus.coordinateType) || remeshData[ChaControl.fileStatus.coordinateType].IsNullOrEmpty()) return;
            PluginData data = new PluginData();
            SetCoordinateExtendedData(coordinate, fillCoordinateData(data));
        }

        protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate)
        {
            if (KKAPI.KoikatuAPI.GetCurrentGameMode() == GameMode.Maker && !KKAPI.Maker.MakerAPI.GetCoordinateLoadFlags().Accessories)
            {
                StartCoroutine(updateMeshesDelay(0.5f));
                return;
            }
            if (remeshData.Keys.Contains(ChaControl.fileStatus.coordinateType))
            {
                remeshData[ChaControl.fileStatus.coordinateType].Clear();
            }

            PluginData data = GetCoordinateExtendedData(coordinate);
            StartCoroutine(applyDataDelayed(1f, data, coordinate));
        }
    }
}
