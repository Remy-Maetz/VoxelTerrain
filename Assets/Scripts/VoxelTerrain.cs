using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways]
public class VoxelTerrain : MonoBehaviour
{
    public Texture2D heightMap;
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

        chunk.material = Instantiate( referenceMaterial );

        var mesh = new Mesh();
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var triangles = new List<int>();

        var pixelOffsetX = cx * chunkSize;
        var pixelOffsetY = cy * chunkSize;
        var blockWidth = Mathf.Min(chunkSize, heightMap.width - pixelOffsetX );
        var blockHeight = Mathf.Min(chunkSize, heightMap.height - pixelOffsetY );

        var deltaX = blockWidth / scale.x;
        var deltaY = blockHeight / scale.z;

        var dx = new Vector3( deltaX, 0, 0);
        var dy = new Vector3( 0, 0, deltaY);
        var dxy = new Vector3(deltaX, 0, deltaY);

        var position = Vector3.zero;
        var dh = Vector3.zero;

        var heights = heightMap.GetPixels(pixelOffsetX, pixelOffsetY, blockWidth, blockHeight, 0);
        var i = 0;
        var pi = 0;

        for (var y = 0; y < blockHeight; y++)
        {
            for (var x = 0; x < blockWidth; x++)
            {
                // top face
                position.y = heights[pi].r * scale.y;
                pi++;

                vertices.Add(position);
                vertices.Add(position+dx);
                vertices.Add(position+dxy);
                vertices.Add(position+dy);

                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);

                triangles.Add(i);
                triangles.Add(i + 2);
                triangles.Add(i+1);
                triangles.Add(i+2);
                triangles.Add(i);
                triangles.Add(i+3);

                i += 4;

                // -z vertical face
                if (y > 0)
                {

                }

                position.x += deltaX;
            }
            position.x = 0;
            position.z += deltaY;
        }

        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
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
        foreach (var chunk in chunks)
        {
            renderParams.layer = gameObject.layer;
            renderParams.renderingLayerMask = GraphicsSettings.defaultRenderingLayerMask;
            renderParams.material = chunk.material;
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

        foreach (var chunk in chunks)
        {
            Gizmos.matrix = chunk.matrix;
            //Gizmos.DrawWireCube(chunksScale * 0.5f, chunksScale);
            Gizmos.DrawWireCube( chunk.mesh.bounds.center , chunk.mesh.bounds.size);
            //Gizmos.DrawWireMesh(chunk.mesh);
        }
    }
}
