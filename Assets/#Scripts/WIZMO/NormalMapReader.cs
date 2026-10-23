//--------------// 法線マップの情報を読み込むスクリプト(髙山)//--------------
using UnityEngine;

public class RoadNormalMapNormalMapReader : MonoBehaviour
{
    [SerializeField] private LayerMask TargetLayer;

    [SerializeField, Range(-0.7f, 1.3f)]
    private float RayCastOffset_X = 0f;

    [SerializeField, Range(-0.7f, 1.3f)]
    private float RayCastOffset_Z = 0;


    void Update()
    {
        SurfaceNormalMap();
    }

    void SurfaceNormalMap()
    {
        Vector3 RayPosition = new Vector3(transform.position.x + RayCastOffset_X, transform.position.y, transform.position.z + RayCastOffset_Z);

        // オブジェクトの下にレイを飛ばす
        Ray ray = new Ray(RayPosition, -transform.up);

        if (Physics.Raycast(ray, out RaycastHit hit, 100f, TargetLayer))
        {            
            if (hit.triangleIndex < 0) return;

            // コリジョンのフィルターとレンダラーコンポーネント取得
            MeshFilter hitmeshFilter = hit.collider.GetComponent<MeshFilter>();
            Renderer hitrenderer = hit.collider.GetComponent<Renderer>();
            //ヌルチェック
            if (hitrenderer == null || hitmeshFilter == null || hitmeshFilter.sharedMesh == null) return;

            // メッシュ取得
            Mesh mesh = hitmeshFilter.sharedMesh;
            if (mesh == null) return;// ヌルチェック


            int subMeshIndex = GetHitSubMeshIndex(mesh, hit.triangleIndex);
            if (subMeshIndex < 0 || subMeshIndex >= hitrenderer.sharedMaterials.Length) return;

            // 衝突したマテリアルがどれなのか取得する
            Material targetMaterial = hitrenderer.sharedMaterials[subMeshIndex];
            if (targetMaterial == null) return;

            // ノーマルマップのプロパティ名
            string TexturePropName = targetMaterial.HasProperty("_NormalMap") ? "_NormalMap" : "_BumpMap";

            // ノーマルマップをピクセル情報が読み取れるようにする
            Texture2D RoadNormalMap = targetMaterial.GetTexture(TexturePropName) as Texture2D;
            if (RoadNormalMap == null) return;

            // UV座標の取得とTiling/Offset適用
            Vector2 rawUV = hit.textureCoord;
            Vector2 Tiling = targetMaterial.GetTextureScale(TexturePropName);
            Vector2 offset = targetMaterial.GetTextureOffset(TexturePropName);

            Vector2 uv = new Vector2(
                Mathf.Repeat(rawUV.x * Tiling.x + offset.x, 1.0f),
                Mathf.Repeat(rawUV.y * Tiling.y + offset.y, 1.0f));

            // ノーマルマップのピクセル色を取得
            Color color = RoadNormalMap.GetPixelBilinear(uv.x, uv.y);

            // 接空間の法線ベクトル
            Vector3 localNormal = UnpackLocalNormal(color);

            // 面法線
            Vector3 worldNormal = hit.normal;

            // メッシュから取得した生の接線
            Vector3 localTangent = GetInterpolatedTangent(mesh, hit);
            Vector3 rawWorldTangent = hit.transform.TransformDirection(localTangent).normalized;

            // 何故か横向きにならないので無理やり張り付ける(あっているのか不明)
            Vector3 worldTangent = Vector3.ProjectOnPlane(rawWorldTangent, worldNormal).normalized;

            
            if (worldTangent.sqrMagnitude < 0.001f)
            {
                worldTangent = Vector3.Cross(worldNormal, Vector3.forward).normalized;
                if (worldTangent.sqrMagnitude < 0.001f)
                {
                    worldTangent = Vector3.Cross(worldNormal, Vector3.right).normalized;
                }
            }

            // 従接）
            Vector3 worldBitangent = Vector3.Cross(worldNormal, worldTangent).normalized;

            // ワールド空間法線の合成
            Vector3 finalWorldNormal = (localNormal.x * worldTangent +
                                        localNormal.y * worldBitangent +
                                        localNormal.z * worldNormal).normalized;


            float RelativeAngle = Vector3.Angle(transform.up, finalWorldNormal);
            Vector3 LocalSelfNormal = transform.InverseTransformDirection(finalWorldNormal);

            float RollAngle = Mathf.Atan2(LocalSelfNormal.x, LocalSelfNormal.y) * Mathf.Rad2Deg;  // 右がプラス、左がマイナス
            float PitchAngle = Mathf.Atan2(LocalSelfNormal.z, LocalSelfNormal.y) * Mathf.Rad2Deg; // 前がプラス、後ろがマイナス

            float angle = Vector3.Angle(worldNormal, finalWorldNormal);

            
            Debug.DrawRay(hit.point, finalWorldNormal * 10.0f, Color.red);
            Debug.DrawRay(hit.point, worldTangent * 0.8f, Color.blue);

            Debug.Log($"【RoadReader】UV:{uv} | RawColor:(R:{color.r:F2}, G:{color.g:F2}, B:{color.b:F2}, A:{color.a:F2}) | LocalNormal:{localNormal} | 傾き:{angle:F2}度");

            Debug.Log($"【NormalMapReader】自分に対する相対傾き:{RollAngle:F2}度 | 左右(Roll):{RollAngle:F2}度 | 前後(Pitch):{RollAngle:F2}度");

        }
    }

    // 取得した色データから傾きを取得する
    private Vector3 UnpackLocalNormal(Color color)
    {
        Vector3 normal = Vector3.zero;

        // ノーマルマップだと圧縮されているらしいので必要なデータを取り出す(X,Y)
        if (color.a != 1.0f || color.r == 1.0f)
        {
            normal.x = color.a * 2.0f - 1.0f;
            normal.y = color.g * 2.0f - 1.0f;
        }
        else
        {
            normal.x = color.r * 2.0f - 1.0f;
            normal.y = color.g * 2.0f - 1.0f;
        }

        // ノーマルマップはZがないらしいので計算して生み出す
        float zSq = 1.0f - normal.x * normal.x - normal.y * normal.y;
        normal.z = Mathf.Sqrt(Mathf.Max(0.0f, zSq));

        return normal.normalized;
    }

    private Vector3 GetInterpolatedTangent(Mesh mesh, RaycastHit hit)
    {
        if (mesh == null || mesh.tangents == null || mesh.tangents.Length == 0 || hit.triangleIndex < 0)
            return Vector3.right;

        int[] triangles = mesh.triangles;
        Vector4[] tangents = mesh.tangents;

        int triangleStart = hit.triangleIndex * 3;

        if (triangles == null || triangleStart + 2 >= triangles.Length)
            return Vector3.right;

        int i0 = triangles[triangleStart + 0];
        int i1 = triangles[triangleStart + 1];
        int i2 = triangles[triangleStart + 2];

        if (i0 >= tangents.Length || i1 >= tangents.Length || i2 >= tangents.Length)
            return Vector3.right;

        Vector3 barycentric = hit.barycentricCoordinate;
        Vector3 interpolatedTangent = (Vector3)tangents[i0] * barycentric.x +
                                      (Vector3)tangents[i1] * barycentric.y +
                                      (Vector3)tangents[i2] * barycentric.z;

        return interpolatedTangent.normalized;
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