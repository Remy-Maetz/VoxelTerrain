using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteAlways] //Required to render the meshes
public class VoxelTerrain : MonoBehaviour
{
    public Texture2D heightMap;
    public Texture2D colorMap;
    public Material referenceMaterial;

    public Vector2 size = new Vector2(256, 256);
    public float height = 64;
    public float heightUVScale = 4;
    public int chunkSize = 16;
    [Range(0.0f, 1.0f)]
    public float heightColorNoiseFactor = 0;
    public float heightColorNoiseScale = 5f;
    public int heightColorNoiseOffset = 0;
    public int heightColorNoiseOctaves = 2;
    public float heightColorNoiseLacunarity = 2f;
    public float heightColorNoisePersistence = 0.3f;

    public enum UVMode
    {
        None,
        OnePixelTop,
        PixelCenter
    };
    public UVMode uvMode = UVMode.OnePixelTop;

    public bool generateColliders = true;

    struct Chunk
    {
        public Mesh mesh;
        public Matrix4x4 matrix;
        public Material material;

        public MeshCollider collider;

        public void GenerateCollider()
        {
            var go = new GameObject(mesh.name);
            // We do not want to save the collider with the scene
            go.hideFlags = HideFlags.DontSave;

            collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }
    }

    private List<Chunk> chunks = new List<Chunk>();

    public void GenerateTerrain()
    {
        if (heightMap == null || colorMap == null)
        {
            Debug.LogWarning("No maps provided for terrain generation.");
            return;
        }

        ClearChunks();

        float chunksXf = heightMap.width * 1.0f / chunkSize;
        float chunksYf = heightMap.height * 1.0f / chunkSize;

        int chunksX = Mathf.CeilToInt(chunksXf);
        int chunksY = Mathf.CeilToInt(chunksYf);

        var chunksSize = new Vector3( size.x / chunksXf, height , size.y / chunksYf);
        var pos = new Vector3(-size.x * 0.5f, -height * 0.5f, -size.y * 0.5f);

        for (var cy = 0; cy < chunksY; cy++)
        {
            for (var cx = 0; cx < chunksX; cx++)
            {
                var chunk = GenerateChunk(cx, cy, chunksSize);
                chunk.matrix = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one);
                chunks.Add(chunk);

                if (generateColliders)
                {
                    chunk.GenerateCollider();
                    chunk.collider.transform.parent = transform;
                    chunk.collider.transform.localPosition = pos;
                    chunk.collider.transform.localRotation = Quaternion.identity;
                    chunk.collider.transform.localScale = Vector3.one;
                }

                pos.x += chunksSize.x;
            }
            pos.x = -size.x * 0.5f;
            pos.z += chunksSize.z;
        }
    }

    Chunk GenerateChunk( int cx, int cy, Vector3 size )
    {
        Chunk chunk = new Chunk();

        bool doXFace = cx > 0;
        bool doZFace = cy > 0;

        chunk.material = Instantiate( referenceMaterial );

        var mesh = new Mesh();
        mesh.hideFlags = HideFlags.DontSave;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();
        var vertexIndex = 0;

        var pixelOffsetX = cx * chunkSize;
        var pixelOffsetY = cy * chunkSize;
        var blockWidth = chunkSize;
        var blockHeight = chunkSize;
        var startX = 0;
        var startY = 0;
        if (doXFace)
        {
            pixelOffsetX--;
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

        var deltaX = size.x / chunkSize;
        var deltaZ = size.z / chunkSize;

        var dx = new Vector3(deltaX, 0, 0);
        var dz = new Vector3(0, 0, deltaZ);
        var dh = Vector3.zero;
        var du = 1.0f / colorMap.width;
        var dv = 1.0f / colorMap.height;

        var position = Vector3.zero;
        var uv_BottomLeft = Vector2.zero;
        var uv_TopRight = Vector2.one;

        var heightBase = 0f;
        float heightDiff, heightMin, heightMax;

        var noiseUV = Vector2.zero;
        var uvOffset = Vector2.zero;
        var maxNoiseValue = 0f;
        var noiseOctaveIntensity = 1f;

        var heights = heightMap.GetPixels(pixelOffsetX, pixelOffsetY, blockWidth, blockHeight, 0);
        var pixelIndex = startY * blockWidth + startX;

        int x, z;

        var bounds = new Bounds();

        void AddQuad( Vector3 origin, Vector3 right, Vector3 up, Vector3 normal, Vector2 uv_BottomLeft, Vector2 uv_TopRight, bool flip = false )
        {
            vertices.Add(origin);
            vertices.Add(origin + right);
            vertices.Add(origin + right + up);
            vertices.Add(origin + up);

            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);

            uvs.Add(uv_BottomLeft + uvOffset);
            uvs.Add(new Vector2(uv_TopRight.x, uv_BottomLeft.y) + uvOffset);
            uvs.Add(uv_TopRight + uvOffset);
            uvs.Add(new Vector2(uv_BottomLeft.x, uv_TopRight.y) + uvOffset);

            if (flip)
            {
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 3);
            }
            else
            {
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex + 1);
                triangles.Add(vertexIndex + 2);
                triangles.Add(vertexIndex);
                triangles.Add(vertexIndex + 3);
            }

            bounds.Encapsulate(vertices[vertexIndex]);
            bounds.Encapsulate(vertices[vertexIndex+1]);
            bounds.Encapsulate(vertices[vertexIndex+2]);
            bounds.Encapsulate(vertices[vertexIndex+3]);

            vertexIndex += 4;
        }

        void ZeroUV() { uv_BottomLeft = uv_TopRight = Vector2.zero; }
        void PixelCenterUV()
        {
            uv_BottomLeft.x = uv_TopRight.x = (cx * chunkSize + x + 0.5f) * 1.0f / heightMap.width;
            uv_BottomLeft.y = uv_TopRight.y = (cy * chunkSize + z + 0.5f) * 1.0f / heightMap.height;
        }

        void AddTopFace( float height )
        {
            position.y = heightBase * size.y;

            switch (uvMode)
            {
                case UVMode.OnePixelTop:
                    uv_BottomLeft.x = Mathf.Floor(heightBase * colorMap.width) * du;
                    uv_TopRight.x = uv_BottomLeft.x + du;
                    uv_BottomLeft.y = 1.0f - dv;
                    uv_TopRight.y = 1.0f;
                    break;
                case UVMode.PixelCenter:
                    PixelCenterUV();
                    break;
                default:
                    ZeroUV();
                    break;
            }

            AddQuad(position, dx, dz, Vector3.up, uv_BottomLeft, uv_TopRight);
        }

        void AddSideFace( Vector3 right, float heightA, float heightB, bool flip )
        {
            heightDiff = heightB - heightA;

            // Don't do the face if the height difference is "about" null
            if (Mathf.Abs(heightDiff) <= Mathf.Epsilon)
                return;

            if (heightDiff > 0)
            {
                heightMax = heightB;
                heightMin = heightA;
            }
            else
            {
                heightMax = heightA;
                heightMin = heightB;
                heightDiff = -heightDiff;
                flip = !flip;
            }

            position.y = heightMin * size.y;
            dh.y = heightDiff * size.y;

            switch (uvMode)
            {
                case UVMode.OnePixelTop:
                    uv_BottomLeft.x = Mathf.Floor(heightMax * colorMap.width) * du;
                    uv_TopRight.x = uv_BottomLeft.x + du;
                    uv_TopRight.y = 1.0f - dv;
                    uv_BottomLeft.y = uv_TopRight.y - heightDiff * heightUVScale;
                    break;
                case UVMode.PixelCenter:
                    PixelCenterUV();
                    break;
                default:
                    ZeroUV();
                    break;
            }


            AddQuad(position, right, dh, Vector3.Cross(right, dh).normalized, uv_BottomLeft, uv_TopRight, flip);
        }

        for (z = startY; z < blockHeight; z++)
        {
            for (x = startX; x < blockWidth; x++)
            {
                noiseUV.x = cx * chunkSize + x;
                noiseUV.y = cy * chunkSize + z;
                noiseUV *= heightColorNoiseScale;
                uvOffset.x = 0;

                maxNoiseValue = 0f;
                noiseOctaveIntensity = 1f;
                for (int i=0; i<heightColorNoiseOctaves; i++)
                {
                    uvOffset.x += (Mathf.PerlinNoise(noiseUV.x, noiseUV.y)*2-1) * noiseOctaveIntensity;
                    noiseUV *= heightColorNoiseLacunarity;
                    maxNoiseValue += noiseOctaveIntensity;
                    noiseOctaveIntensity *= heightColorNoisePersistence;
                }
                uvOffset.x /= maxNoiseValue;

                uvOffset.x = uvOffset.x * heightColorNoiseFactor * heightColorNoiseOffset;
                uvOffset.x = Mathf.Round(uvOffset.x);
                uvOffset.x *= du;

                // top face
                heightBase = heights[pixelIndex].r;
                AddTopFace(heightBase);

                // -x vertical face
                if (x > 0 || doXFace)
                {
                    AddSideFace(dz, heightBase, heights[pixelIndex - 1].r, false);
                }

                // -z vertical face
                if (z > 0 || doZFace)
                {
                    AddSideFace(dx, heightBase, heights[pixelIndex - blockWidth].r, true);
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
        mesh.bounds = bounds;
        mesh.name = $"Terrain_{cx}_{cy}";

        chunk.mesh = mesh;

        return chunk;
    }

    void ClearChunks()
    {
        foreach ( var chunk in chunks )
        {
            Object.DestroyImmediate(chunk.mesh);
            Object.DestroyImmediate(chunk.material);
            if (chunk.collider != null)
                Object.DestroyImmediate (chunk.collider.gameObject);
        }
        chunks.Clear();

        // Clear any remaining child objects
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(transform.GetChild(i).gameObject);
        }
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
            Graphics.RenderMesh( renderParams, chunk.mesh, 0, transform.localToWorldMatrix * chunk.matrix);
        }
    }

    private void Update()
    {
        RenderTerrain();
    }

    private void Start()
    {
        GenerateTerrain();
    }

    //This will recreate the terrain on domain reload
    private void OnEnable()
    {
        if (chunks.Count == 0)
            GenerateTerrain();
    }

    [System.Flags]
    public enum GizmosModes
    {
        None = 0,
        Bound = 1,
        Mesh = 2
    };
    [HideInInspector]
    public GizmosModes gizmoMode = GizmosModes.None;

    private void OnDrawGizmosSelected()
    {
        if (gizmoMode == GizmosModes.None) return;

        var chunksScale = new Vector3(size.x / chunkSize, height, size.y / chunkSize);
        Random.InitState( 42 );
        foreach (var chunk in chunks)
        {
            Gizmos.color = Color.HSVToRGB(Random.value, 1, 1);
            Gizmos.matrix = transform.localToWorldMatrix * chunk.matrix;

            if (gizmoMode.HasFlag(GizmosModes.Bound)) Gizmos.DrawWireCube( chunk.mesh.bounds.center , chunk.mesh.bounds.size);
            if (gizmoMode.HasFlag(GizmosModes.Mesh)) Gizmos.DrawWireMesh(chunk.mesh);
        }
    }
}
