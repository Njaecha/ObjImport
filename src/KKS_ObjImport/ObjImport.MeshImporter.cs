// borrowed from https://github.com/eastskykang/UnityMeshImporter/blob/master/Runtime/Scripts/MeshImporter.cs
// edited to fit my purposes

/* 
 *  MIT License
 *  
 *  Copyright (c) 2019 UnityMeshImporter - Dongho Kang
 *  
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *  
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *  
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */

using System;
using System.Collections.Generic;
using System.IO;
using Assimp;
using UnityEngine;
using Material = UnityEngine.Material;
using Mesh = UnityEngine.Mesh;

namespace ObjImport
{
    class MeshMaterialBinding
    {
        private string meshName;
        private UnityEngine.Mesh mesh;
        private UnityEngine.Material material;

        private MeshMaterialBinding() { }    // Do not allow default constructor

        public MeshMaterialBinding(string meshName, Mesh mesh, Material material)
        {
            this.meshName = meshName;
            this.mesh = mesh;
            this.material = material;
        }

        public Mesh Mesh { get => mesh; }
        public Material Material { get => material; }
        public string MeshName { get => meshName; }
    }

    public class MeshImporter
    {
        /// <summary>
        /// Loads the meshes from a 3D file using AssimpNet
        /// </summary>
        /// <param name="meshPath">filePath</param>
        /// <returns></returns>
        public static List<Mesh> ImportFile(string meshPath)
        {
            if (!File.Exists(meshPath))
                return null;

            AssimpContext importer = new AssimpContext();
            Scene scene = importer.ImportFile(meshPath);
            if (scene == null)
                return null;

            string parentDir = Directory.GetParent(meshPath).FullName;


            // Mesh
            List<Mesh> meshes = new List<Mesh>();
            if (scene.HasMeshes)
            {
                foreach (var mesh in scene.Meshes)
                {
                    List<Vector3> uVertices = new List<Vector3>();
                    List<Vector3> uNormals = new List<Vector3>();
                    List<Vector2> uUv = new List<Vector2>();
                    List<int> uIndices = new List<int>();

                    // Vertices
                    if (mesh.HasVertices)
                    {
                        foreach (var v in mesh.Vertices)
                        {
                            uVertices.Add(new Vector3(-v.X, v.Y, v.Z));
                        }
                    }

                    // Normals
                    if (mesh.HasNormals)
                    {
                        foreach (var n in mesh.Normals)
                        {
                            uNormals.Add(new Vector3(-n.X, n.Y, n.Z));
                        }
                    }

                    // Triangles
                    if (mesh.HasFaces)
                    {
                        foreach (var f in mesh.Faces)
                        {
                            // Ignore degenerate faces
                            if (f.IndexCount == 1 || f.IndexCount == 2)
                                continue;

                            for (int i = 0; i < (f.IndexCount - 2); i++)
                            {
                                uIndices.Add(f.Indices[i + 2]);
                                uIndices.Add(f.Indices[i + 1]);
                                uIndices.Add(f.Indices[0]);
                            }
                        }
                    }

                    // Uv (texture coordinate) 
                    if (mesh.HasTextureCoords(0))
                    {
                        foreach (var uv in mesh.TextureCoordinateChannels[0])
                        {
                            uUv.Add(new Vector2(uv.X, uv.Y));
                        }
                    }

                    UnityEngine.Mesh uMesh = new UnityEngine.Mesh();
                    uMesh.vertices = uVertices.ToArray();
                    uMesh.normals = uNormals.ToArray();
                    uMesh.triangles = uIndices.ToArray();
                    uMesh.uv = uUv.ToArray();

                    int num = 0;

                    if (mesh.Name != "")
                        uMesh.name = mesh.Name;
                    else
                    {
                        uMesh.name = meshPath.Substring(meshPath.LastIndexOf("/")).Remove(0, 1) + num;
                        num++;
                    }

                    meshes.Add(uMesh);
                }
            }
            return null;
        }

        // privated for now due to not using this
        private static GameObject Load(string meshPath, float scaleX = 1, float scaleY = 1, float scaleZ = 1)
        {
            if (!File.Exists(meshPath))
                return null;

            AssimpContext importer = new AssimpContext();
            Scene scene = importer.ImportFile(meshPath);
            if (scene == null)
                return null;

            string parentDir = Directory.GetParent(meshPath).FullName;

            // Materials
            List<UnityEngine.Material> uMaterials = new List<Material>();
            if (scene.HasMaterials)
            {
                foreach (var material in scene.Materials)
                {
                    Shader shader = Shader.Find("Standard");
                    UnityEngine.Material uMaterial = new UnityEngine.Material(shader);

                    // Albedo
                    if (material.HasColorDiffuse)
                    {
                        Color color = new Color(
                            material.ColorDiffuse.R,
                            material.ColorDiffuse.G,
                            material.ColorDiffuse.B,
                            material.ColorDiffuse.A
                        );
                        uMaterial.color = color;
                    }

                    // Emission
                    if (material.HasColorEmissive)
                    {
                        Color color = new Color(
                            material.ColorEmissive.R,
                            material.ColorEmissive.G,
                            material.ColorEmissive.B,
                            material.ColorEmissive.A
                        );
                        uMaterial.SetColor("_EmissionColor", color);
                        uMaterial.EnableKeyword("_EMISSION");
                    }

                    // Reflectivity
                    if (material.HasReflectivity)
                    {
                        uMaterial.SetFloat("_Glossiness", material.Reflectivity);
                    }

                    // Texture
                    if (material.HasTextureDiffuse)
                    {
                        ObjImport.Logger.LogInfo("Attempting to load textures");

                        Texture2D uTexture = new Texture2D(2, 2);
                        string texturePath = Path.Combine(parentDir, material.TextureDiffuse.FilePath);

                        if (File.Exists(texturePath))
                        {

                            byte[] byteArray = File.ReadAllBytes(texturePath);
                            bool isLoaded = uTexture.LoadImage(byteArray);
                            if (!isLoaded)
                            {
                                throw new Exception("Cannot find texture file: " + texturePath);
                            }

                            uMaterial.SetTexture("_MainTex", uTexture);
                        }
                        else
                        {
                            ObjImport.Logger.LogWarning($"Texture [{texturePath}] does not exist");
                        }
                    }

                    uMaterials.Add(uMaterial);
                }
            }

            // Mesh
            List<MeshMaterialBinding> uMeshAndMats = new List<MeshMaterialBinding>();
            if (scene.HasMeshes)
            {
                foreach (var mesh in scene.Meshes)
                {
                    List<Vector3> uVertices = new List<Vector3>();
                    List<Vector3> uNormals = new List<Vector3>();
                    List<Vector2> uUv = new List<Vector2>();
                    List<int> uIndices = new List<int>();

                    // Vertices
                    if (mesh.HasVertices)
                    {
                        foreach (var v in mesh.Vertices)
                        {
                            uVertices.Add(new Vector3(-v.X, v.Y, v.Z));
                        }
                    }

                    // Normals
                    if (mesh.HasNormals)
                    {
                        foreach (var n in mesh.Normals)
                        {
                            uNormals.Add(new Vector3(-n.X, n.Y, n.Z));
                        }
                    }

                    // Triangles
                    if (mesh.HasFaces)
                    {
                        foreach (var f in mesh.Faces)
                        {
                            // Ignore degenerate faces
                            if (f.IndexCount == 1 || f.IndexCount == 2)
                                continue;

                            for (int i = 0; i < (f.IndexCount - 2); i++)
                            {
                                uIndices.Add(f.Indices[i + 2]);
                                uIndices.Add(f.Indices[i + 1]);
                                uIndices.Add(f.Indices[0]);
                            }
                        }
                    }

                    // Uv (texture coordinate) 
                    if (mesh.HasTextureCoords(0))
                    {
                        foreach (var uv in mesh.TextureCoordinateChannels[0])
                        {
                            uUv.Add(new Vector2(uv.X, uv.Y));
                        }
                    }

                    UnityEngine.Mesh uMesh = new UnityEngine.Mesh();
                    uMesh.vertices = uVertices.ToArray();
                    uMesh.normals = uNormals.ToArray();
                    uMesh.triangles = uIndices.ToArray();
                    uMesh.uv = uUv.ToArray();

                    uMeshAndMats.Add(new MeshMaterialBinding(mesh.Name, uMesh, uMaterials[mesh.MaterialIndex]));
                }
            }

            // Create GameObjects from nodes
            GameObject NodeToGameObject(Node node)
            {
                GameObject uOb = new GameObject(node.Name);

                // Set Mesh
                if (node.HasMeshes)
                {
                    foreach (var mIdx in node.MeshIndices)
                    {
                        var uMeshAndMat = uMeshAndMats[mIdx];

                        GameObject uSubOb = new GameObject(uMeshAndMat.MeshName);
                        uSubOb.AddComponent<MeshFilter>();
                        uSubOb.AddComponent<MeshRenderer>();
                        uSubOb.AddComponent<MeshCollider>();

                        uSubOb.GetComponent<MeshFilter>().mesh = uMeshAndMat.Mesh;
                        uSubOb.GetComponent<MeshRenderer>().material = uMeshAndMat.Material;
                        uSubOb.transform.SetParent(uOb.transform, true);
                        uSubOb.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);

                        //set layer 10 for koi
                        uSubOb.layer = 10;

                    }
                }

                // Transform
                // Decompose Assimp transform into scale, rot and translaction 
                Assimp.Vector3D aScale = new Assimp.Vector3D();
                Assimp.Quaternion aQuat = new Assimp.Quaternion();
                Assimp.Vector3D aTranslation = new Assimp.Vector3D();
                node.Transform.Decompose(out aScale, out aQuat, out aTranslation);

                // Convert Assimp transfrom into Unity transform and set transformation of game object 
                UnityEngine.Quaternion uQuat = new UnityEngine.Quaternion(aQuat.X, aQuat.Y, aQuat.Z, aQuat.W);
                var euler = uQuat.eulerAngles;
                uOb.transform.localScale = new UnityEngine.Vector3(aScale.X, aScale.Y, aScale.Z);
                uOb.transform.localPosition = new UnityEngine.Vector3(aTranslation.X, aTranslation.Y, aTranslation.Z);
                uOb.transform.localRotation = UnityEngine.Quaternion.Euler(euler.x, -euler.y, euler.z);

                if (node.HasChildren)
                {
                    foreach (var cn in node.Children)
                    {
                        var uObChild = NodeToGameObject(cn);
                        uObChild.transform.SetParent(uOb.transform, false);
                    }
                }

                // set to layer 10 for koi
                uOb.layer = 10;

                return uOb;
            }

            return NodeToGameObject(scene.RootNode); ;
        }
    }
}
