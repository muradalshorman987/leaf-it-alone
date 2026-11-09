using UnityEngine;

namespace FallenYard.Leaves
{
    /// <summary>
    /// Builds a tiny double-sided low-poly leaf mesh so the game works before
    /// any Blender assets exist. Replace with your own mesh on the LeafField.
    /// </summary>
    public static class LeafMeshFactory
    {
        public static Mesh CreateLeafMesh()
        {
            // Diamond-ish leaf, 1 unit long on Z, slightly raised spine.
            Vector3[] top =
            {
                new Vector3(0f, 0f, 0.5f),      // tip
                new Vector3(0.24f, 0.02f, 0.05f), // right
                new Vector3(0f, 0.05f, 0f),      // spine
                new Vector3(-0.24f, 0.02f, 0.05f),// left
                new Vector3(0f, 0f, -0.5f),     // stem
            };
            Vector2[] topUv =
            {
                new Vector2(0.5f, 1f), new Vector2(1f, 0.55f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0.55f), new Vector2(0.5f, 0f),
            };
            int[] topTris = { 0, 1, 2, 0, 2, 3, 2, 1, 4, 3, 2, 4 };

            var verts = new Vector3[10];
            var uvs = new Vector2[10];
            var normals = new Vector3[10];
            for (int i = 0; i < 5; i++)
            {
                verts[i] = top[i];
                verts[i + 5] = top[i] - new Vector3(0f, 0.002f, 0f);
                uvs[i] = uvs[i + 5] = topUv[i];
                normals[i] = Vector3.up;
                normals[i + 5] = Vector3.down;
            }

            var tris = new int[topTris.Length * 2];
            for (int i = 0; i < topTris.Length; i += 3)
            {
                tris[i] = topTris[i];
                tris[i + 1] = topTris[i + 1];
                tris[i + 2] = topTris[i + 2];
                // back face: reversed winding, offset into the second vertex set
                tris[topTris.Length + i] = topTris[i] + 5;
                tris[topTris.Length + i + 1] = topTris[i + 2] + 5;
                tris[topTris.Length + i + 2] = topTris[i + 1] + 5;
            }

            var mesh = new Mesh { name = "ProceduralLeaf" };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
