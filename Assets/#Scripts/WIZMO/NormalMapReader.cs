//--------------
// 法線マップの情報を読み込むスクリプト(髙山)
//--------------

using UnityEngine;

public class RoadNormalMapNormalMapReader : MonoBehaviour
{     
    [SerializeField] private LayerMask TargetLayer; 

   void Start()
    {
        
    }

     
    void Update()
    {
        SurfaceNormalMap();
    }

    void SurfaceNormalMap()
    {
        // オブジェクトの下にレイを飛ばす
        Ray ray = new Ray(transform.position, Vector3.down);



        if (Physics.Raycast(ray, out RaycastHit hit, 100, TargetLayer))
        {
            // 衝突したオブジェクトのメッシュ　
            MeshFilter hitmeshFilter = hit.collider.GetComponent<MeshFilter>();

            // 衝突したオブジェクトのレンダラー
            Renderer hitrenderer = hit.collider.GetComponent<Renderer>();

            Mesh mesh = hitmeshFilter.sharedMesh;
            // ヌルチェック
            if (hitrenderer == null)
            {

                return;// ないならリターン
            }
            if (hitmeshFilter == null)
            {

                return;// ないならリターン
            }

            if (mesh == null)
            {

                return;// ないならリターン
            }

            if (hitmeshFilter.sharedMesh == null)
            {

                return;// ないならリターン
            }


            // レイがヒットしたマテリアルの番号を計算
            int subMeshIndex = GetHitSubMeshIndex(hitmeshFilter.sharedMesh, hit.triangleIndex);

            // 配列外の処理はreturn
            if (subMeshIndex >= hitrenderer.sharedMaterials.Length) return;

            // メッシュの情報からマテリアルの配列を取得
            Material targetMaterial = hitrenderer.sharedMaterials[subMeshIndex];

            //  マテリアルからnormalマップを取得
            Texture2D RoadNormalMap = targetMaterial.GetTexture("_NormalMap") as Texture2D;
            if (RoadNormalMap == null)
            {
                return;// ないならリターン
            }

            // UV座標の取得
            Vector2 uv = hit.textureCoord;

            // ノーマルマップのピクセル色を取得
            Color color = RoadNormalMap.GetPixelBilinear(uv.x, uv.y);

            // 接空間の法線ベクトルに変換
            Vector3 localNormal = new Vector3(color.r * 2.0f - 1.0f, color.g * 2.0f - 1.0f, color.b * 2.0f - 1.0f).normalized;

            // ワールド空間の法線に変換して傾きを計算
            Vector3 worldNormal = GetInterpolatedTangent(mesh,hit);
            Vector3 worldTangent = hit.transform.TransformDirection(worldNormal);
            Vector3 worldBitangent = Vector3.Cross(worldNormal, worldTangent).normalized;

            Vector3 finalWorldNormal = (localNormal.x * worldTangent +
                                        localNormal.y * worldBitangent +
                                        localNormal.z * worldNormal).normalized;


            float angle = Vector3.Angle(worldNormal, finalWorldNormal);



            Debug.DrawRay(hit.point, finalWorldNormal * 0.5f, Color.red);
            Debug.Log($"【RoadNormalMapNormalMapReader】対象: {hit.collider.name} | 傾き: {angle:F2}度");

        }
    }

    // 当たったポリゴンの正確な接線ベクトルに補正
    private Vector3 GetInterpolatedTangent(Mesh mesh, RaycastHit hit)
    {
        // ヌルチェック
        if (mesh == null || mesh.tangents == null || mesh.tangents.Length == 0)
            return hit.transform.right;


        int[] triangles = mesh.triangles;
        Vector4[] tangents = mesh.tangents;

        int triangleStart = hit.triangleIndex * 3;

        int i0 = triangles[triangleStart + 0];
        int i1 = triangles[triangleStart + 1];
        int i2 = triangles[triangleStart + 2];

        if (i0 >= tangents.Length || i1 >= tangents.Length || i2 >= tangents.Length)
        {
            return Vector3.right;
        }

        Vector3 barycentric = hit.barycentricCoordinate;
        Vector3 interpolatedTangent = tangents[i0] * barycentric.x +
                                      tangents[i1] * barycentric.y +
                                      tangents[i2] * barycentric.z;

        return interpolatedTangent.normalized;
    }

    private int GetHitSubMeshIndex(Mesh mesh, int triangleIndex)
    {
        int accumulatedTriangles = 0;

        for (int i = 0; i < mesh.subMeshCount; i++)
        {
            int numIndices = (int) mesh.GetIndexCount(i);
            int numTriangles = numIndices / 3;

            if(triangleIndex < accumulatedTriangles + numTriangles)
            {
                return i;
            }

            accumulatedTriangles += numTriangles;

        }
        return 0;
    }
}
