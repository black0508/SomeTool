using UnityEngine;

namespace WanRen
{
    // 这个是构建针的Mesh
    public static class NeedleMeshFactory
    {
        public static Mesh CreateNeedle()
        {
            const int sides = 8;
            var mesh = new Mesh { name = "Needle" };
            var vertices = new Vector3[sides * 4 + 2];
            var normals = new Vector3[vertices.Length];
            var triangles = new int[sides * 21];

            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                var rim = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices[i] = rim * 0.35f + new Vector3(0f, 0.5f, 0f);
                vertices[sides + i] = rim * 0.18f + new Vector3(0f, 0.22f, 0f);
                vertices[sides * 2 + i] = rim * 0.18f + new Vector3(0f, -0.28f, 0f);
                vertices[sides * 3 + i] = rim * 0.04f + new Vector3(0f, -0.42f, 0f);
                normals[i] = rim;
                normals[sides + i] = rim;
                normals[sides * 2 + i] = rim;
                normals[sides * 3 + i] = (rim + Vector3.down).normalized;
            }

            int top = sides * 4;
            int tip = sides * 4 + 1;
            vertices[top] = new Vector3(0f, 0.5f, 0f);
            vertices[tip] = new Vector3(0f, -0.5f, 0f);
            normals[top] = Vector3.up;
            normals[tip] = Vector3.down;

            int t = 0;
            for (int i = 0; i < sides; i++)
            {
                int n = (i + 1) % sides;
                t = Quad(triangles, t, i, n, sides + n, sides + i);
                t = Quad(triangles, t, sides + i, sides + n, sides * 2 + n, sides * 2 + i);
                t = Quad(triangles, t, sides * 2 + i, sides * 2 + n, sides * 3 + n, sides * 3 + i);
                triangles[t++] = sides * 3 + i;
                triangles[t++] = sides * 3 + n;
                triangles[t++] = tip;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static int Quad(int[] triangles, int t, int a, int b, int c, int d)
        {
            triangles[t++] = a;
            triangles[t++] = b;
            triangles[t++] = c;
            triangles[t++] = a;
            triangles[t++] = c;
            triangles[t++] = d;
            return t;
        }
    }
}
