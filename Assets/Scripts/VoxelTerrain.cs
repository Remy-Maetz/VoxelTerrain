using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class VoxelTerrain : MonoBehaviour
{
    public Texture2D heightMap;
    public Texture2D colorMap;
    public Material referenceMaterial;

    public Vector2 size = new Vector2(256, 256);
    public float height = 64;
    public int chunkSize = 16;

    public bool drawGizmos = true;

    public bool regenerate = false;

    struct Chunk
    {
        public Mesh mesh;
        public Matrix4x4 matrix;
        public Material material;
    }

    private List<Chunk> chunks = new List<Chunk>();

    void GenerateTerrain()
    {
        ClearChunks();

        int chunksX = Mathf.CeilToInt(size.x / chunkSize);
        int chunksY = Mathf.CeilToInt(size.y / chunkSize);

        var chunksScale = new Vector3( size.x / chunkSize, height , size.y / chunkSize );
        var pos = new Vector3(-size.x * 0.5f, -height * 0.5f, -size.y * 0.5f);

        for (var cy = 0; cy < chunksY; cy++)
        {
            for (var cx = 0; cx < chunksX; cx++)
            {
                var chunk = GenerateChunk(cx, cy, chunksScale);
                chunk.matrix = Matrix4x4.TRS(transform.TransformPoint(pos), transform.rotation, transform.lossyScale);
                chunks.Add(chunk);
                pos.x += chunksScale.x;
            }
            pos.x = -size.x * 0.5f;
            pos.z += chunksScale.z;
        }
    }

    Chunk GenerateChunk( int cx, int cy, Vector3 scale )
    {
        Chunk chunk = new Chunk();

        bool doXFace = cx > 0;
        bool doZFace = cy > 0;

        chunk.material = Instantiate( referenceMaterial );

        var mesh = new Mesh();
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        var vertexIndex = 0;

        void AddQuad( Vector3 origin, Vector3 right, Vector3 up, Vector3 normal, Vector2 uv_BottomLeft, Vector2 uv_TopRight )
        {
            vertices.Add(origin);
            vertices.Add(origin + right);
            vertices.Add(origin + right + up);
            vertices.Add(origin + up);

            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);

            uvs.Add(uv_BottomLeft);
            uvs.Add(new Vector2(uv_TopRight.x, uv_BottomLeft.y));
            uvs.Add(uv_TopRight);
            uvs.Add(new Vector2(uv_BottomLeft.x, uv_TopRight.y));

            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex + 1);
            triangles.Add(vertexIndex + 2);
            triangles.Add(vertexIndex);
            triangles.Add(vertexIndex + 3);

            vertexIndex += 4;
        }

        var pixelOffsetX = cx * chunkSize;
        var pixelOffsetY = cy * chunkSize;
        var blockWidth = chunkSize;
        var blockHeight = chunkSize;
        var startX = 0;
        var startY = 0;
        if (doXFace)
        {
            pixelOffsetX --;
            blockWidth++;
            startX++;
        }
        if (doZFace)
        {
            pixelOffsetY--;
            blockHeight++;
            startY++;
        }
        blockWidth = Mathf.Min(blockWidth, heightMap.width - pixelOffsetX);
        blockHeight = Mathf.Min(blockHeight, heightMap.height - pixelOffsetY);

        var deltaX = chunkSize / scale.x;
        var deltaZ = chunkSize / scale.z;

        var dx = new Vector3( deltaX, 0, 0);
        var dz = new Vector3( 0, 0, deltaZ);
        var dh = Vector3.zero;
        var du = 1.0f / colorMap.width;
        var dv = 1.0f / colorMap.height;

        var position = Vector3.zero;
        var uv_BottomLeft = Vector2.zero;
        var uv_TopRight = Vector2.one;

        var heights = heightMap.GetPixels(pixelOffsetX, pixelOffsetY, blockWidth, blockHeight, 0);
        var pixelIndex = startY * blockWidth + startX;

        for (var z = startY; z < blockHeight; z++)
        {
            for (var x = startX; x < blockWidth; x++)
            {
                // top face
                position.y = heights[pixelIndex].r * scale.y;

                uv_BottomLeft.x = Mathf.Floor(heights[pixelIndex].r * colorMap.width)*du;
                uv_TopRight.x = uv_BottomLeft.x + du;
                uv_BottomLeft.y = 1.0f - dv;
                uv_TopRight.y = 1.0f;

                AddQuad(position, dx, dz, Vector3.up, uv_BottomLeft, uv_TopRight);

                // -x vertical face
                if (x > 0 || doXFace)
                {
                    dh.y = heights[pixelIndex-1].r * scale.y - position.y;
                    if (Mathf.Abs(dh.y) > Mathf.Epsilon)
                        AddQuad(position, dz, dh, Vector3.left, uv_BottomLeft, uv_TopRight);
                }

                // -z vertical face
                if (z > 0 || doZFace)
                {
                    dh.y = heights[pixelIndex - blockWidth].r * scale.y - position.y;
                    if (Mathf.Abs(dh.y) > Mathf.Epsilon)
                        AddQuad(position+dx, -dx, dh, Vector3.back, uv_BottomLeft, uv_TopRight);
                }

                pixelIndex++;
                position.x += deltaX;
            }
            // Offset the pixel index to skip first pixel of a line if we are also doing the first X face of the chunk
            if (doXFace)
            {
                pixelIndex++;
            }
            position.x = 0;
            position.z += deltaZ;
        }

        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateBounds();

        chunk.mesh = mesh;

        return chunk;
    }

    void ClearChunks()
    {
        foreach ( var chunk in chunks )
        {
            Object.DestroyImmediate(chunk.mesh);
            Object.DestroyImmediate(chunk.material);
        }
        chunks.Clear();
    }

    void RenderTerrain()
    {
        var renderParams = new RenderParams();
        renderParams.layer = gameObject.layer;
        renderParams.renderingLayerMask = GraphicsSettings.defaultRenderingLayerMask;
        renderParams.material = referenceMaterial;
        renderParams.shadowCastingMode = ShadowCastingMode.On;
        renderParams.receiveShadows = true;
        foreach (var chunk in chunks)
        {
            Graphics.RenderMesh( renderParams, chunk.mesh, 0, chunk.matrix);
        }
    }

    private void Update()
    {
        RenderTerrain();
    }

    private void OnValidate()
    {
        if (regenerate)
            regenerate = false;

        GenerateTerrain();
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;

        var chunksScale = new Vector3(size.x / chunkSize, height, size.y / chunkSize);
        Random.InitState( 42 );
        foreach (var chunk in chunks)
        {
            Gizmos.color = Color.HSVToRGB(Random.value, 1, 1);
            Gizmos.matrix = chunk.matrix;
            //Gizmos.DrawWireCube(chunksScale * 0.5f, chunksScale);
            //Gizmos.DrawWireCube( chunk.mesh.bounds.center , chunk.mesh.bounds.size);
            Gizmos.DrawWireMesh(chunk.mesh);
        }
    }
}
