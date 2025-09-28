/// "borrowed" from https://github.com/aedenthorn/ValheimMods/blob/master/CustomMeshes

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Logging;
using KK_Plugins.MaterialEditor;
using MaterialEditorAPI;
using UnityEngine;
using UnityEngine.PostProcessing;

namespace ObjImport
{
    public class ObjImporter
    {
        private ManualLogSource Logger;

        public ObjImporter(ManualLogSource Logger)
        {
            this.Logger = Logger;
        }

        public Mesh convertToMesh(MeshDto data, bool is32bit)
        {

            if (is32bit)
            {
                Logger.LogMessage("ERROR: Mesh has too many vertices (>65k).");
                return null;
            }

            populateMeshStruct(ref data);
            Vector3[] newVerts = new Vector3[data.faceData.Length];
            Vector2[] newUVs = new Vector2[data.faceData.Length];
            Vector3[] newNormals = new Vector3[data.faceData.Length];
            
            try
            {
                int i = 0;
                foreach (Vector3 v in data.faceData)
                {
                    newVerts[i] = data.vertices[(int)v.x - 1];
                    if (v.y >= 1)
                        newUVs[i] = data.uv[(int)v.y - 1];
                    if (v.z >= 1)
                        newNormals[i] = data.normals[(int)v.z - 1];
                    i++;
                }
            }
            catch (Exception ex)
            {
                Logger.LogMessage("ERROR: Obj files seems to contain multi model information, use Multi-Object Mode!");
                return null;
            }
            

            Mesh mesh = new Mesh();
            mesh.name = data.name;
            mesh.vertices = newVerts;
            mesh.uv = newUVs;
            mesh.normals = newNormals;
            mesh.triangles = data.triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        // Original ImportFile unchanged except it calls createMeshStruct/populateMeshStruct
        public MeshDto ImportFile(string filePath)
        {
            
            try
            {
                MeshDto newMesh = createMeshStruct(filePath);
                return newMesh;
            }
            catch (Exception error)
            {
                Logger.LogError($"An error occurred on importing the obj: {error}");
                return null;
            }
        }

        // Extended parser to catch mtllib + usemtl
        private static MeshDto createMeshStruct(string filename)
        {
            int triangles = 0, vertices = 0, vt = 0, vn = 0, face = 0;
            MeshDto mesh = new MeshDto(); 

            mesh.fileName = filename;
            mesh.usedMaterial = "default";

            foreach (var line in File.ReadAllLines(filename))
            {
                string currentText = line.Trim();
                if (currentText.StartsWith("mtllib "))
                {
                    mesh.mtlFile = currentText.Substring(7).Trim();
                }
                else if (currentText.StartsWith("usemtl "))
                {
                    mesh.usedMaterial = currentText.Substring(7).Trim();
                }
                else if (currentText.StartsWith("v ")) vertices++;
                else if (currentText.StartsWith("vt ")) vt++;
                else if (currentText.StartsWith("vn ")) vn++;
                else if (currentText.StartsWith("f "))
                {
                    string[] brokenString = currentText.Split(' ');
                    face += brokenString.Length - 1;
                    triangles += 3 * (brokenString.Length - 2);
                }
            }

            //ObjImport.Logger.LogMessage("mesh.usedMaterial: " + mesh.usedMaterial);
            mesh.triangles = new int[triangles];
            mesh.vertices = new Vector3[vertices];
            mesh.uv = new Vector2[vt];
            mesh.normals = new Vector3[vn];
            mesh.faceData = new Vector3[face];
            return mesh;
        }

        // I have to subtract this from the number that the obj saves for each vertex of a face
        // because the obj refers to its entire list and I only have the verts of the current object
        private static int usedUpVerts = 0;
        private static int usedUpNorms = 0;
        private static int usedUpUVs = 0;
        private static void populateMeshStruct(ref MeshDto mesh)
        {
            StreamReader stream = File.OpenText(mesh.fileName);
           
            string objectText = stream.ReadToEnd();
            stream.Close();

            using (StringReader reader = new StringReader(objectText))
            {
                string currentText = reader.ReadLine();

                char[] splitIdentifier = { ' ' };
                char[] splitIdentifier2 = { '/' };
                string[] brokenString;
                string[] brokenBrokenString;
                int f = 0;
                int f2 = 0;
                int v = 0;
                int vn = 0;
                int vt = 0;
                int vt1 = 0;
                int vt2 = 0;
                bool skippedFirstO = false;
                while (currentText != null)
                {
                    if (!currentText.StartsWith("f ") && !currentText.StartsWith("v ") && !currentText.StartsWith("vt ") &&
                        !currentText.StartsWith("vn ") && !currentText.StartsWith("g ") && !currentText.StartsWith("usemtl ") &&
                        !currentText.StartsWith("mtllib ") && !currentText.StartsWith("vt1 ") && !currentText.StartsWith("vt2 ") &&
                        !currentText.StartsWith("vc ") && !currentText.StartsWith("usemap ") && !currentText.StartsWith("o "))
                    {
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");
                        }
                    }
                    else
                    {
                        currentText = currentText.Trim();
                        brokenString = currentText.Split(splitIdentifier);
                        switch (brokenString[0])
                        {
                            case "g":
                                break;
                            case "usemtl":
                                break;
                            case "usemap":
                                break;
                            case "mtllib":
                                break;
                            case "v":
                                mesh.vertices[v] = new Vector3(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]),
                                                         System.Convert.ToSingle(brokenString[3]));
                                v++;
                                break;
                            case "vt":
                                mesh.uv[vt] = new Vector2(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]));
                                vt++;
                                break;
                            case "vt1":
                                mesh.uv[vt1] = new Vector2(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]));
                                vt1++;
                                break;
                            case "vt2":
                                mesh.uv[vt2] = new Vector2(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]));
                                vt2++;
                                break;
                            case "vn":
                                mesh.normals[vn] = new Vector3(System.Convert.ToSingle(brokenString[1]), System.Convert.ToSingle(brokenString[2]),
                                                        System.Convert.ToSingle(brokenString[3]));
                                vn++;
                                break;
                            case "vc":
                                break;
                            case "f":
                                int j = 1;
                                List<int> intArray = new List<int>();
                                while (j < brokenString.Length && ("" + brokenString[j]).Length > 0)
                                {
                                    Vector3 temp = new Vector3();
                                    brokenBrokenString = brokenString[j].Split(splitIdentifier2, 3);    //Separate the face into individual components (vert, uv, normal)
                                    temp.x = System.Convert.ToInt32(brokenBrokenString[0]) - usedUpVerts; //subtract number of vertieces used by other objects before
                                    if (brokenBrokenString.Length > 1)                                  //Some .obj files skip UV and normal
                                    {
                                        if (brokenBrokenString[1] != "")                                //Some .obj files skip the uv and not the normal
                                        {
                                            temp.y = System.Convert.ToInt32(brokenBrokenString[1]) - usedUpUVs; //subtract number of UVs used by other objects before
                                        }
                                        if (brokenBrokenString.Length > 2)                              //Some .obj files miss the normal completly
                                        {
                                            temp.z = System.Convert.ToInt32(brokenBrokenString[2]) - usedUpNorms; //subtract number of Normals used by other objects before
                                        }
                                    }
                                    j++;

                                    mesh.faceData[f2] = temp;
                                    intArray.Add(f2);
                                    f2++;
                                }
                                j = 1;
                                while (j + 2 < brokenString.Length)     //Create triangles out of the face data.  There will generally be more than 1 triangle per face.
                                {
                                    mesh.triangles[f] = intArray[0];
                                    f++;
                                    mesh.triangles[f] = intArray[j];
                                    f++;
                                    mesh.triangles[f] = intArray[j + 1];
                                    f++;

                                    j++;
                                }
                                break;
                            case "o":
                                if (skippedFirstO)
                                {
                                    usedUpVerts += v; //add the amount of vertices this object has to the usedUpVerts
                                    usedUpNorms += vn; //add the amount of normals this object has to the usedUpNorms
                                    usedUpUVs += vt; //add the amount of UVs this object has to the usedUpUVs
                                    return;
                                }
                                else
                                {
                                    mesh.name = brokenString[1];
                                    skippedFirstO = true;
                                }
                                break;
                        }
                        currentText = reader.ReadLine();
                        if (currentText != null)
                        {
                            currentText = currentText.Replace("  ", " ");       //Some .obj files insert double spaces, this removes them.
                        }
                    }
                }
            }
        }
    }
}