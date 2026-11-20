using UnityEngine;
using UnityEngine.UIElements;

public class RoadHeightMapReader : MonoBehaviour
{

    [SerializeField] private LayerMask TargetLayer;

    [SerializeField] private Transform vehicleRoot;

    [Header("タイヤオブジェクト")]
    [SerializeField] private Transform FriontRightWheel;
    [SerializeField] private Transform FriontLeftWheel;
    [SerializeField] private Transform BackRightWheel;
    [SerializeField] private Transform BackLeftWheel;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        SurfaceHeigjtlMap(FriontRightWheel);
    }

    private void SurfaceHeigjtlMap(Transform WheelTransfom)
    {
        // 車とホイールがないならリターンしよう
        if (!WheelTransfom || !vehicleRoot)
        {
            Debug.Log("RoadHeightMapReader:Nokuruma");
            return;
        }
        
        // 基準のトランスフォーム
        Transform baseTransform = vehicleRoot;

        // レイを飛ばす場所
        Vector3 RayPosition = WheelTransfom.position + baseTransform.up;

        // オブジェクトの下にレイを飛ばす
        Ray ray = new Ray(RayPosition,-baseTransform.up);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, TargetLayer))
        {

            // コリジョンのフィルターとレンダラーコンポーネント取得
            MeshFilter hitmeshFilter = hit.collider.GetComponent<MeshFilter>();

            Renderer hitrenderer = hit.collider.GetComponent<Renderer>();

            // メッシュ取得
            Mesh mesh = hitmeshFilter.sharedMesh;
            if (mesh == null)
            {
                Debug.Log("RoadHeightMapReader:NoMesh");
                return;// ヌルチェック
            }


            int subMeshIndex = GetHitSubMeshIndex(mesh, hit.triangleIndex);
            if (subMeshIndex < 0 || subMeshIndex >= hitrenderer.sharedMaterials.Length)
            {
                Debug.Log("RoadHeightMapReader:fdssfsd");
                return;
            }


            Material targetMaterial = hitrenderer.sharedMaterials[subMeshIndex];
            if (targetMaterial == null)
            {
                Debug.Log("RoadHeightMapReader:NotargetMaterial");
                return;
            }

            string TexterPropName = targetMaterial.HasProperty("_HeightMap") ? "_HeightMap" : "_HeightMap";

            // マテリアルから Height Map (または _HeightMap) を取得
            Texture2D heightMap = targetMaterial.GetTexture(TexterPropName) as Texture2D;
            if (heightMap != null)
            {
                Vector2 rawUV = hit.textureCoord;

                Vector2 Tiling = targetMaterial.GetTextureScale(TexterPropName);
                Vector2 offset = targetMaterial.GetTextureOffset(TexterPropName);

                Vector2 uv = new Vector2(
                    Mathf.Repeat(rawUV.x * Tiling.x + offset.x, 1.0f),
                    Mathf.Repeat(rawUV.y * Tiling.y + offset.y, 1.0f));



                Color color = heightMap.GetPixelBilinear(uv.x, uv.y);
                float brightness = color.grayscale; // 濃淡（0.0〜1.0
                                                    // 濃淡に応じた物理処理

                Debug.Log("RoadHeightMapReader:道の濃淡" + brightness);

                if (brightness > 0.5f)
                {
                    // 白っぽい領域の処理
                }
            }

            else
            {
                Debug.Log("RoadHeightMapReader:heigtMapないっす");
            }
        }

        else
        {
           Debug.Log("RoadHeightMapReader:うまく行ってはいないらしい");
        }

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
