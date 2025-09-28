using System.Collections.Generic;
using UnityEngine;

namespace ObjImport
{
    public class MaterialDto
    {
        // new
        public string mtlFile;
        public string usedMaterial;
    }

    public class MeshDto : MaterialDto
    {
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector2[] uv;
        public int[] triangles;
        public Vector3[] faceData;
        public string name;
        public string fileName;

    }

    public class MeshAdvancedDto : MeshDto
    {
        public Vector2[] uv1;
        public Vector2[] uv2;
        public int[] faceVerts;
        public int[] faceUVs;
        public int startLine;
    }

}