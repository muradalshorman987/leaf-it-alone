using UnityEngine;

namespace FallenYard.Leaves
{
    /// <summary>
    /// Tiny double-sided low-poly leaf meshes so the game works before any Blender
    /// assets exist (WP6 replaces them). Every mesh is 1 unit long on Z, centred on
    /// the origin, and at most 20 triangles counting both faces.
    /// </summary>
    public static class LeafMeshFactory
    {
        /// <summary>The default (oak-like) leaf: a diamond with a raised spine. 8 triangles.</summary>
        public static Mesh CreateLeafMesh()
        {
            Vector3[] top =
            {
                new Vector3(0f, 0f, 0.5f),        // tip
                new Vector3(0.24f, 0.02f, 0.05f), // right
                new Vector3(0f, 0.05f, 0f),       // spine
                new Vector3(-0.24f, 0.02f, 0.05f),// left
                new Vector3(0f, 0f, -0.5f),       // stem
            };
            Vector2[] uv =
            {
                new Vector2(0.5f, 1f), new Vector2(1f, 0.55f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0.55f), new Vector2(0.5f, 0f),
            };
            int[] tris = { 0, 1, 2, 0, 2, 3, 2, 1, 4, 3, 2, 4 };
            return BuildDoubleSided("ProceduralLeaf", top, uv, tris);
        }

        /// <summary>Wide, pointed five-lobe leaf. 20 triangles.</summary>
        public static Mesh CreateMapleMesh()
        {
            // Fan around a centre vertex: tip, lobes and notches alternate.
            Vector3[] top =
            {
                new Vector3(0f, 0.05f, 0.05f),     // 0 centre
                new Vector3(0f, 0f, 0.5f),         // 1 top lobe tip
                new Vector3(0.12f, 0.02f, 0.2f),   // 2 notch
                new Vector3(0.45f, 0f, 0.25f),     // 3 upper right lobe
                new Vector3(0.18f, 0.02f, 0.02f),  // 4 notch
                new Vector3(0.35f, 0f, -0.25f),    // 5 lower right lobe
                new Vector3(0f, 0.01f, -0.18f),    // 6 base (stem)
                new Vector3(-0.35f, 0f, -0.25f),   // 7 lower left lobe
                new Vector3(-0.18f, 0.02f, 0.02f), // 8 notch
                new Vector3(-0.45f, 0f, 0.25f),    // 9 upper left lobe
                new Vector3(-0.12f, 0.02f, 0.2f),  // 10 notch
            };
            var uv = new Vector2[top.Length];
            for (int i = 0; i < top.Length; i++) uv[i] = new Vector2(top[i].x + 0.5f, top[i].z + 0.5f);
            int[] tris =
            {
                0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 6,
                0, 6, 7, 0, 7, 8, 0, 8, 9, 0, 9, 10, 0, 10, 1,
            };
            return BuildDoubleSided("ProceduralMapleLeaf", top, uv, tris);
        }

        /// <summary>A lumpy, roughly round wad of wet leaves. 14 triangles.</summary>
        public static Mesh CreateClumpMesh()
        {
            Vector3[] top =
            {
                new Vector3(0f, 0.18f, 0f),         // 0 crown
                new Vector3(0f, 0f, 0.45f),         // 1
                new Vector3(0.35f, 0.04f, 0.3f),    // 2
                new Vector3(0.48f, 0f, -0.05f),     // 3
                new Vector3(0.25f, 0.03f, -0.4f),   // 4
                new Vector3(-0.2f, 0f, -0.45f),     // 5
                new Vector3(-0.45f, 0.04f, -0.1f),  // 6
                new Vector3(-0.3f, 0f, 0.32f),      // 7
            };
            var uv = new Vector2[top.Length];
            for (int i = 0; i < top.Length; i++) uv[i] = new Vector2(top[i].x + 0.5f, top[i].z + 0.5f);
            int[] tris = { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 5, 0, 5, 6, 0, 6, 7, 0, 7, 1 };
            // The underside is flat so the clump sits on the ground.
            return BuildDoubleSided("ProceduralWetClump", top, uv, tris, underside: true);
        }

        /// <summary>A small fan of three thin needles. 6 triangles.</summary>
        public static Mesh CreateNeedleMesh()
        {
            const float w = 0.025f;
            var top = new Vector3[9];
            float[] angles = { -25f, 0f, 25f };
            for (int n = 0; n < 3; n++)
            {
                Quaternion r = Quaternion.Euler(0f, angles[n], 0f);
                top[n * 3 + 0] = r * new Vector3(-w, 0.01f, -0.5f);
                top[n * 3 + 1] = r * new Vector3(w, 0.01f, -0.5f);
                top[n * 3 + 2] = r * new Vector3(0f, 0f, 0.5f);
            }
            var uv = new Vector2[top.Length];
            for (int i = 0; i < top.Length; i++) uv[i] = new Vector2(i % 3 == 2 ? 0.5f : i % 3, i % 3 == 2 ? 1f : 0f);
            int[] tris = { 0, 2, 1, 3, 5, 4, 6, 8, 7 };
            return BuildDoubleSided("ProceduralPineNeedles", top, uv, tris);
        }

        /// <summary>Flat double-sided quad for distant leaves. 4 triangles.</summary>
        public static Mesh CreateLodQuad()
        {
            Vector3[] top =
            {
                new Vector3(-0.22f, 0.01f, -0.45f), new Vector3(0.22f, 0.01f, -0.45f),
                new Vector3(0.22f, 0.01f, 0.45f), new Vector3(-0.22f, 0.01f, 0.45f),
            };
            Vector2[] uv = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            int[] tris = { 0, 2, 1, 0, 3, 2 };
            return BuildDoubleSided("ProceduralLeafLod", top, uv, tris);
        }

        public static Mesh Create(LeafPreset shape)
        {
            switch (shape)
            {
                case LeafPreset.Maple: return CreateMapleMesh();
                case LeafPreset.WetClump: return CreateClumpMesh();
                case LeafPreset.PineNeedles: return CreateNeedleMesh();
                default: return CreateLeafMesh();
            }
        }

        /// <summary>
        /// Builds a mesh from the top surface plus a copy for the back face (reversed winding,
        /// normals down). Top triangles must wind clockwise seen from above (Unity's front face).
        /// </summary>
        static Mesh BuildDoubleSided(string name, Vector3[] top, Vector2[] uv, int[] topTris, bool underside = false)
        {
            int n = top.Length;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var normals = new Vector3[n * 2];
            for (int i = 0; i < n; i++)
            {
                verts[i] = top[i];
                Vector3 back = top[i] - new Vector3(0f, 0.002f, 0f);
                if (underside) back.y = 0f;
                verts[i + n] = back;
                uvs[i] = uvs[i + n] = uv[i];
                normals[i] = Vector3.up;
                normals[i + n] = Vector3.down;
            }

            var tris = new int[topTris.Length * 2];
            for (int i = 0; i < topTris.Length; i += 3)
            {
                tris[i] = topTris[i];
                tris[i + 1] = topTris[i + 1];
                tris[i + 2] = topTris[i + 2];
                tris[topTris.Length + i] = topTris[i] + n;
                tris[topTris.Length + i + 1] = topTris[i + 2] + n;
                tris[topTris.Length + i + 2] = topTris[i + 1] + n;
            }

            var mesh = new Mesh { name = name };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
