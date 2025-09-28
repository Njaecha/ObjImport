using System.Collections.Generic;
using SysDiag = System.Diagnostics;
using System.IO;
using UnityEngine;
using System;
using static ObjImport.ObjImporter;
using BepInEx.Logging;

namespace ObjImport
{
    public class MtlData
    {
        public string name;
        public string diffuseTexPath;
        public Color diffuseColor = Color.white;
        public Color ambientColor = Color.white;
        public Color specularColor = Color.white;
        public Color emissionColor = Color.black;
        public float shininess = 0.0f;
        public float refractionIndex = 1.0f;
        public float transparency = 1.0f;
        public int illuminationModel = 2;  // Default value for illumination model
        public string shaderKey = "Standard";

        public Texture2D texture;
    }

    public class MaterialImporter : MonoBehaviour
    {

        public Dictionary<string, MtlData> meshMaterialMap = new Dictionary<string, MtlData>();
        private ManualLogSource Logger;

        public MaterialImporter(ManualLogSource Logger)
        {
            this.Logger = Logger;
            this.meshMaterialMap = new Dictionary<string, MtlData>();
        }

        public MtlData findMaterials(string key)
        {
            if (meshMaterialMap.ContainsKey(key))
            {
                return meshMaterialMap[key];
            }
            return null;
        }


        public void importMaterials(string filePath, List<MeshAdvancedDto> datas)
        {
            foreach (MeshAdvancedDto data in datas)
            {
                importMaterials(filePath, data);
            }

            Logger.LogMessage("Loaded " + meshMaterialMap.Count + " materials for " + datas.Count + " Meshes");


            /* shows available shaders
            foreach (string key in KK_Plugins.MaterialEditor.MaterialEditorPlugin.LoadedShaders.Keys)
            {
                Logger.LogMessage("Shader: " + key);
            }
            */
        }

        public void importMaterials(string filePath, MeshDto data)
        {
            if(data.mtlFile != null)
            {
                string mtlPath = Path.Combine(Path.GetDirectoryName(filePath), data.mtlFile);
                if (File.Exists(mtlPath))
                {
                    LoadMtl(mtlPath);
                }
            }
            
        }

        // Load MTL file and create Unity materials
        private void LoadMtl(string mtlPath)
        {
            MtlData current = null;
            foreach (var line in File.ReadAllLines(mtlPath))
            {
                string l = line.Trim();

                // New material definition
                if (l.StartsWith("newmtl "))
                {
                    current = new MtlData();
                    current.shaderKey = ObjImport.selectedShaderKey; //shaderKey from the ui selection
                    current.name = l.Substring(7).Trim();
                    if (!this.meshMaterialMap.ContainsKey(current.name))
                    {
                        this.meshMaterialMap[current.name] = current;
                    }
                }
                // Diffuse color (Kd)
                else if (l.StartsWith("Kd "))
                {
                    string[] parts = l.Split(' ');
                    if (parts.Length >= 4)
                    {
                        float r = float.Parse(parts[1]);
                        float g = float.Parse(parts[2]);
                        float b = float.Parse(parts[3]);
                        current.diffuseColor = new Color(r, g, b);
                    }
                }
                // Ambient color (Ka)
                else if (l.StartsWith("Ka "))
                {
                    string[] parts = l.Split(' ');
                    if (parts.Length >= 4)
                    {
                        float r = float.Parse(parts[1]);
                        float g = float.Parse(parts[2]);
                        float b = float.Parse(parts[3]);
                        current.ambientColor = new Color(r, g, b);
                    }
                }
                // Specular color (Ks)
                else if (l.StartsWith("Ks "))
                {
                    string[] parts = l.Split(' ');
                    if (parts.Length >= 4)
                    {
                        float r = float.Parse(parts[1]);
                        float g = float.Parse(parts[2]);
                        float b = float.Parse(parts[3]);
                        current.specularColor = new Color(r, g, b);
                    }
                }
                // Emission color (Ke)
                else if (l.StartsWith("Ke "))
                {
                    string[] parts = l.Split(' ');
                    if (parts.Length >= 4)
                    {
                        float r = float.Parse(parts[1]);
                        float g = float.Parse(parts[2]);
                        float b = float.Parse(parts[3]);
                        current.emissionColor = new Color(r, g, b);
                    }
                }
                // Refraction index (Ni)
                else if (l.StartsWith("Ni "))
                {
                    string[] parts = l.Split(' ');
                    if (parts.Length >= 2)
                    {
                        current.refractionIndex = float.Parse(parts[1]);
                    }
                }
                // Transparency factor (d)
                else if (l.StartsWith("d "))
                {
                    string[] parts = l.Split(' ');
                    if (parts.Length >= 2)
                    {
                        current.transparency = float.Parse(parts[1]);
                    }
                }
                // Illumination model (illum)
                else if (l.StartsWith("illum "))
                {
                    string[] parts = l.Split(' ');
                    if (parts.Length >= 2)
                    {
                        current.illuminationModel = int.Parse(parts[1]);
                    }
                }
                // Diffuse texture map (map_Kd)
                else if (l.StartsWith("map_Kd "))
                {
                    string texFile = l.Substring(7).Trim();
                    string texPath = Path.Combine(Path.GetDirectoryName(mtlPath), texFile);
                    current.diffuseTexPath = texPath;
                }
            }

            // After parsing the MTL file, load the texture for each material
            foreach (MtlData data in this.meshMaterialMap.Values)
            {
                if (!string.IsNullOrEmpty(data.name))
                {
                    data.texture = LoadTexture(data);
                }
            }
        }

        private Texture2D LoadTexture(MtlData data)
        {
            if (!string.IsNullOrEmpty(data.diffuseTexPath) && File.Exists(data.diffuseTexPath))
            {
                byte[] fileData = File.ReadAllBytes(data.diffuseTexPath);
                Texture2D tex = KKAPI.Utilities.TextureUtils.LoadTexture(fileData);
                return tex;
            }
            return null;
        }

        public void FillMaterial(Material mat, MtlData data)
        {
            // Try to find Koikatsu's common item shader first
            Shader shader = KK_Plugins.MaterialEditor.MaterialEditorPlugin.LoadedShaders[
                data.shaderKey
            ].Shader;

            if (shader == null)
                shader = Shader.Find("Standard"); // fallback

            mat.shader = shader;

            mat.name = data.name;

            // Apply diffuse color
            mat.color = data.diffuseColor;

            // Apply specular color
            mat.SetColor("_SpecColor", data.specularColor);

            // Apply ambient color (if needed)
            mat.SetColor("_AmbientColor", data.ambientColor);

            // Apply emission color (if any)
            mat.SetColor("_EmissionColor", data.emissionColor);
            if (data.emissionColor != Color.black)
            {
                mat.EnableKeyword("_EMISSION");
            }

            // Apply transparency
            mat.SetFloat("_Mode", data.transparency < 1.0f ? 3.0f : 0.0f); // 3.0f for transparency (cutout)
            mat.SetFloat("_Transparency", data.transparency);

            //KKUSS specific settings
            if (shader.name.Contains("KKUSS"))
            {
                mat.SetColor("_Addcolor", data.diffuseColor);
                mat.SetFloat("_ShadowPower", 0.1f);
            }

            //KKUTS specific settings
            if (shader.name.Contains("KKUTS"))
            {
                mat.SetColor("_BaseColor", data.diffuseColor);
                Color darkerShadeColor = new Color( //darker shade
                    data.diffuseColor.r - 0.2f > 0 ? data.diffuseColor.r - 0.2f : 0.0f,
                    data.diffuseColor.g - 0.2f > 0 ? data.diffuseColor.g - 0.2f : 0.0f,
                    data.diffuseColor.b - 0.2f > 0 ? data.diffuseColor.b - 0.2f : 0.0f
                );

                mat.SetColor("_1st_ShadeColor", darkerShadeColor);
                mat.SetColor("_2nd_ShadeColor", darkerShadeColor);
            }

            // Apply texture if present
            mat.mainTexture = data.texture;

        }
    }
}