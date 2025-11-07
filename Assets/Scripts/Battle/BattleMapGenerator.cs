using UnityEngine;
using System.Collections.Generic;

// 戦闘シーン(BattleScene)がロードされたときに、
// MainSceneのマップ構造や敵の配置（戦闘対象以外）を
// GameManagerから受け取ったデータに基づいて視覚的に「復元」するためのクラスです。
public class BattleMapGenerator : MonoBehaviour
{
    // --- MainSceneのMapGeneratorと共通のプレハブ ---
    // (インスペクタから、BattleScene用のプレハブ（またはMainSceneと同じプレハブ）を設定する必要があります)
    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject wallPrefabView;
    public GameObject stairsPrefab;
    public GameObject chestPrefab;
    public GameObject enemyPrefab; // 戦闘対象「以外」の敵を復元するために使用
    public GameObject openWallPrefab;

    // 生成したマップオブジェクトをまとめる親オブジェクト
    private GameObject mapHolder;
    // 宝箱の管理リスト（このシーンでは主に表示用。開ける機能はMainScene側が持つ）
    private Dictionary<Vector2Int, List<GameObject>> chestObjectLists = new Dictionary<Vector2Int, List<GameObject>>();


    void Start()
    {
        // GameManagerが存在しないと、復元すべきデータがないため処理を中断
        if (GameManager.instance == null)
        {
            Debug.LogError("GameManagerが見つかりません。MainSceneから開始してください。");
            return;
        }

        // --- マップ構造の復元 ---
        if (GameManager.instance.mapData != null)
        {
            // 1. GameManagerに保存されているマップデータ(mapData)のクローン（コピー）を
            //    MainSceneのMapGeneratorが使っている静的変数(MapGenerator.map)にも設定します。
            //    (注: MiniMapDisplayがこの静的変数を参照しているため、戦闘シーンでもマップを表示するために必要)
            MapGenerator.map = (int[,])GameManager.instance.mapData.Clone();

            // 2. 受け取ったマップデータに基づいて、壁や床などのオブジェクトを生成します。
            GenerateMapFromData(GameManager.instance.mapData);
        }

        // --- 戦闘対象「以外」の敵の配置を復元 ---
        // GameManagerに保存されている「残りの敵の位置リスト(enemyPositions)」に基づいて、
        // enemyPrefab を配置します。
        PlaceObjects("Enemy", enemyPrefab, GameManager.instance.enemyPositions);
    }

    // GameManagerから渡されたマップデータ(map)に基づいて、マップオブジェクトを生成します。
    // (MainSceneのMapGenerator.GenerateMap とほぼ同じですが、敵(タイプ4)の生成は行いません)
    void GenerateMapFromData(int[,] map)
    {
        // 生成するオブジェクト群をまとめるための親オブジェクトを作成
        mapHolder = new GameObject("BattleMapHolder");
        // 宝箱リストを初期化
        chestObjectLists.Clear();

        // --- 高さ1の層 (壁, 宝箱, 開く壁) を生成 ---
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                // マップ座標(0～15)をワールド座標(-7.5f～7.5f)に変換
                float posX = x - 7.5f;
                float posZ = z - 7.5f;

                // 1: 壁
                if (map[z, x] == 1)
                {
                    Vector3 wallPosition = new Vector3(posX, 1f, posZ);
                    Instantiate(wallPrefab, wallPosition, Quaternion.identity, mapHolder.transform);
                }
                // 3: 宝箱
                else if (map[z, x] == 3)
                {
                    // (MainSceneで開けられていたとしても、ここではデータに基づいて復元・表示されます)
                    Vector2Int key = new Vector2Int(x, z);
                    if (!chestObjectLists.ContainsKey(key))
                    {
                        chestObjectLists[key] = new List<GameObject>();
                    }
                    Vector3 chestPosition = new Vector3(posX, 4.5f, posZ);
                    GameObject chestInstance = Instantiate(chestPrefab, chestPosition, Quaternion.identity, mapHolder.transform);
                    chestObjectLists[key].Add(chestInstance);
                }
                // 5: 開く壁
                else if (map[z, x] == 5)
                {
                    // (MainSceneで既に開けられていた場合、mapDataは '0' になっているはずなので、
                    //  ここが実行されるのは「まだ開けられていない」場合のみ)
                    if (openWallPrefab == null)
                    {
                        Debug.LogError("BattleMapGeneratorに openWallPrefab が設定されていません！");
                        continue;
                    }
                    Vector3 wallPosition = new Vector3(posX, 1f, posZ);
                    Instantiate(openWallPrefab, wallPosition, Quaternion.identity, mapHolder.transform);
                }
                // (タイプ4: 敵 はここでは生成しない。PlaceObjectsで別途配置するため)
            }
        }

        // --- 高さ0の層 (床, 階段) を生成 ---
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                float posX = x - 7.5f;
                float posZ = z - 7.5f;
                Vector3 wallPosition = new Vector3(posX, 0f, posZ);
                Vector3 stairsPosition = new Vector3(posX, -0.55f, posZ); // 階段用の位置調整
                // 2: 階段
                if (map[z, x] == 2)
                {
                    Instantiate(stairsPrefab, stairsPosition, Quaternion.identity, mapHolder.transform);
                }
                // それ以外: 床
                else
                {
                    Instantiate(floorPrefab, wallPosition, Quaternion.identity, mapHolder.transform);
                }
            }
        }

        // --- 高さ2の層 (天井) を生成 ---
        // (MainSceneの見た目と合わせるため、全面に配置)
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                float posX = x - 7.5f;
                float posZ = z - 7.5f;
                Vector3 wallPosition = new Vector3(posX, 2f, posZ);
                Instantiate(floorPrefab, wallPosition, Quaternion.identity, mapHolder.transform);
            }
        }

        // --- 高さ5の層 (上層階の床/天井) を生成 ---
        // (MainSceneの見た目と合わせるため)
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                float posX = x - 7.5f;
                float posZ = z - 7.5f;
                Vector3 wallPosition = new Vector3(posX, 5f, posZ);
                Vector3 stairsPosition = new Vector3(posX, -4.55f, posZ); // 見えない位置
                // 2: 階段 (見えない位置に配置)
                if (map[z, x] == 2)
                {
                    Instantiate(stairsPrefab, stairsPosition, Quaternion.identity, mapHolder.transform);
                }
                // それ以外: 上層階の床 (wallPrefabView)
                else
                {
                    Instantiate(wallPrefabView, wallPosition, Quaternion.identity, mapHolder.transform);
                }
            }
        }
    }

    // 指定されたプレハブ(prefab)を、指定された位置リスト(positions)に基づいて一括で配置するヘルパーメソッド。
    // (主に戦闘対象「以外」の敵を復元するために使用)
    void PlaceObjects(string tag, GameObject prefab, List<Vector3> positions)
    {
        if (positions == null || prefab == null) return;

        // 生成したオブジェクトをまとめる親オブジェクトを作成 (例: "EnemyHolder")
        var objectHolder = new GameObject(tag + "Holder");

        // リスト内のすべての座標(pos)にプレハブを生成
        foreach (var pos in positions)
        {
            Instantiate(prefab, pos, Quaternion.identity, objectHolder.transform);
        }
    }
}