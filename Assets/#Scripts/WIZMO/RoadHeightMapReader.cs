using UnityEngine;
using UnityEngine.UIElements;

public class RoadHeightMapReader : MonoBehaviour
{

    [SerializeField] private LayerMask TargetLayer;

    [SerializeField, Range(-0.7f, 1.3f)]
    private float RayCastOffset_X = 0f;

    [SerializeField, Range(-0.7f, 1.3f)]
    private float RayCastOffset_Z = 0;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 RayPosition = new Vector3(transform.position.x + RayCastOffset_X, transform.position.y, transform.position.z + RayCastOffset_Z);

        // オブジェクトの下にレイを飛ばす
        Ray ray = new Ray(RayPosition, -transform.up);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, TargetLayer))
        {

            // コリジョンのフィルターとレンダラーコンポーネント取得
            MeshFilter hitmeshFilter = hit.collider.GetComponent<MeshFilter>();

            Renderer hitrenderer = hit.collider.GetComponent<Renderer>();

            // メッシュ取得
            Mesh mesh = hitmeshFilter.sharedMesh;
            if (mesh == null) return;// ヌルチェック


            int subMeshIndex = GetHitSubMeshIndex(mesh, hit.triangleIndex);
            if (subMeshIndex < 0 || subMeshIndex >= hitrenderer.sharedMaterials.Length) return;


            Material targetMaterial = hitrenderer.sharedMaterials[subMeshIndex];
            if (targetMaterial == null) return;



            // マテリアルから Height Map (または _HeightMap) を取得
            Texture2D heightMap = targetMaterial.GetTexture("_HeightMap") as Texture2D;
        }
        //if (heightMap != null)
        //{
        //    Color color = heightMap.GetPixelBilinear(uv.x, uv.y);
        //    float brightness = color.grayscale; // 濃淡（0.0〜1.0
        //    // 濃淡に応じた物理処理
        //    if (brightness > 0.5f)
        //    {
        //        // 白っぽい領域の処理
        //    }
        //}
    }

    private int GetHitSubMeshIndex(Mesh mesh, int triangleIndex)
    {
        if (mesh == null || triangleIndex < 0) return 0;

        int accumulatedTriangles = 0;

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            int numIndices = (int)mesh.GetIndexCount(i);
            int numTriangles = numIndices / 3;

            if (triangleIndex < accumulatedTriangles + numTriangles)
            {
                return i;
            }

            accumulatedTriangles += numTriangles;
        }
        return 0;
    }

}
