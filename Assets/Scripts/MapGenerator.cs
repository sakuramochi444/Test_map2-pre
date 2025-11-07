using System.Collections.Generic;
using UnityEngine;

// マップデータの配列に基づいて、壁、床、敵、宝箱などのオブジェクトをシーン上に生成（インスタンス化）します。
// また、宝箱の開閉状態を管理し、すべての宝箱が開けられたら特定の壁（openWall）を削除する機能も持ちます。
public class MapGenerator : MonoBehaviour
{
    // MapGeneratorのシングルトンインスタンス
    public static MapGenerator instance;

    // --- インスペクタから設定するプレハブ ---
    public GameObject wallPrefab; // 壁 (Y=1)
    public GameObject floorPrefab; // 床 (Y=0) / 天井 (Y=2)
    public GameObject wallPrefabView; // 上層階の床 (Y=5)
    public GameObject StairsPrefab; // 階段
    public GameObject ChestPrefab; // 宝箱
    public GameObject EnemyPrefab; // 敵
    public GameObject openWallPrefab; // 宝箱をすべて開けると開く壁 (タイプ5)

    // マップ座標(Key=Vector2Int)と、そこに配置された宝箱(Value=List<GameObject>)を紐付けます。
    private Dictionary<Vector2Int, List<GameObject>> chestObjectLists = new Dictionary<Vector2Int, List<GameObject>>();

    // マップ座標(Key=Vector2Int)と、そこに配置された「開く壁」(Value=List<GameObject>)を紐付けます。
    private Dictionary<Vector2Int, List<GameObject>> openWallObjectLists = new Dictionary<Vector2Int, List<GameObject>>();

    // --- 宝箱の管理 ---
    private int totalChestsInCurrentMap = 0; // 現在のマップに存在する宝箱の総数
    private int openedChestsInCurrentMap = 0; // 現在のマップで開けた宝箱の数

    // 現在のマップデータを保持する静的配列 (16x16)
    // 0: 道, 1: 壁, 2: 階段, 3: 宝箱, 4: 敵, 5: 開く壁
    public static int[,] map = new int[16, 16];

    // --- マップデータ定義 ---
    // (レベルごとのマップデータは静的配列として保持します)
    #region Map Level Definitions
    public static int[,] level1 = new int[16, 16]
    {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,1,1,1,0,1,3,0,0,0,0,0,1,1,1,1},
        {1,1,1,1,0,1,1,1,0,1,1,3,1,1,1,1},
        {1,1,1,1,0,1,0,0,0,0,1,1,1,1,1,1},
        {1,1,1,1,0,1,1,1,1,0,0,0,1,1,1,1},
        {1,1,1,1,0,0,0,0,1,0,1,1,1,1,1,1},
        {1,1,1,1,0,1,1,0,1,0,1,3,1,1,1,1},
        {1,1,1,1,5,1,0,0,0,0,1,0,1,1,1,1},
        {1,1,1,1,2,1,3,0,1,0,0,0,1,1,1,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    };

    public static int[,] level2 = new int[16, 16]
    {   
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,1,0,0,0,1,1,3,1,1,1,1,1,0,1,1},
        {1,1,0,2,0,1,1,0,1,1,1,1,1,0,1,1},
        {1,1,0,0,0,1,1,0,0,4,0,1,0,0,1,1},
        {1,1,1,5,1,1,1,1,0,1,0,1,0,1,1,1},
        {1,1,0,0,0,0,1,1,0,1,0,1,0,1,1,1},
        {1,1,0,4,0,0,0,0,0,1,0,1,4,3,1,1},
        {1,1,0,1,1,1,1,1,0,4,0,0,0,1,1,1},
        {1,1,0,1,1,1,1,0,0,1,0,1,0,1,1,1},
        {1,1,0,1,3,0,0,0,1,1,0,1,0,1,1,1},
        {1,1,0,1,1,1,1,1,1,1,0,1,0,1,1,1},
        {1,1,0,1,1,0,0,0,0,4,0,1,3,1,1,1},
        {1,1,0,0,0,0,3,1,1,1,0,1,1,1,1,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    };

    public static int[,] level3 = new int[16, 16]
    {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,1,3,1,1,1,1,1,4,0,1,1,1,0,1,1},
        {1,1,0,0,0,0,0,5,0,2,1,1,0,0,1,1},
        {1,1,0,1,1,1,1,1,1,1,1,1,1,0,1,1},
        {1,1,0,1,3,0,0,1,0,0,0,0,0,0,1,1},
        {1,1,0,1,1,1,0,1,0,1,0,1,1,0,1,1},
        {1,1,0,0,1,0,0,1,1,1,3,1,0,0,1,1},
        {1,1,1,0,1,0,4,1,3,1,1,1,1,0,1,1},
        {1,1,4,0,1,1,0,1,0,0,0,0,0,0,1,1},
        {1,1,0,1,3,1,0,1,1,1,1,1,1,0,1,1},
        {1,1,0,1,0,0,0,0,0,0,0,0,0,4,1,1},
        {1,1,0,1,1,1,1,1,1,1,1,1,1,0,1,1},
        {1,1,0,0,0,0,0,0,0,0,0,0,0,0,1,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    };


    public static int[,] level4 = new int[16, 16]
    {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,1,0,0,0,0,1,1,1,4,1,0,0,0,1},
        {1,0,0,0,1,1,0,0,1,1,0,5,0,2,0,1},
        {1,1,1,1,1,1,1,0,1,1,0,1,0,0,0,1},
        {1,1,0,0,0,0,0,4,1,1,0,1,1,1,1,1},
        {1,1,0,1,1,1,1,0,1,1,0,0,1,4,3,1},
        {1,0,0,0,3,1,1,0,1,1,1,0,1,0,1,1},
        {1,0,1,0,1,1,0,0,0,1,1,0,1,0,0,1},
        {1,0,1,4,0,1,0,1,0,0,1,0,1,1,0,1},
        {1,0,1,1,0,0,0,0,1,0,0,0,0,0,0,1},
        {1,4,0,1,1,0,1,0,1,1,0,1,1,1,1,1},
        {1,1,0,0,0,0,1,0,1,0,0,4,0,1,3,1},
        {1,1,1,1,1,0,1,0,1,0,1,1,3,1,0,1},
        {1,3,1,1,4,0,1,0,1,1,1,1,1,1,0,1},
        {1,0,0,0,0,1,1,0,0,0,0,0,0,0,4,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    };


    public static int[,] level5 = new int[16, 16]
    {
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1},
        {1,0,0,0,0,0,0,0,0,0,0,0,0,0,0,1},
        {1,0,1,1,1,1,1,1,1,0,1,1,1,1,0,1},
        {1,0,0,1,0,0,0,0,0,4,0,1,1,1,0,1},
        {1,1,0,1,1,0,1,1,1,1,0,0,0,0,0,1},
        {1,0,0,0,1,0,1,0,0,0,1,0,1,1,4,1},
        {1,0,1,4,0,0,0,0,1,0,1,3,0,1,0,1},
        {1,0,1,3,1,1,1,0,0,4,1,1,0,1,0,1},
        {1,0,0,1,1,1,1,1,1,0,0,0,0,1,0,1},
        {1,1,0,0,0,0,0,1,0,1,1,1,0,0,0,1},
        {1,1,1,1,1,0,1,4,0,0,0,0,1,3,1,1},
        {1,3,0,0,4,0,0,0,1,1,1,4,0,1,1,1},
        {1,1,1,1,1,0,1,0,0,0,1,1,0,0,0,1},
        {1,2,0,0,1,0,1,1,1,0,1,1,1,1,0,1},
        {1,0,0,0,5,4,0,0,0,0,1,3,0,4,0,1},
        {1,1,1,1,1,1,1,1,1,1,1,1,1,1,1,1}
    };
    #endregion

    // --- 階層管理 ---

    // 全マップレベルのデータを保持するリスト (静的)
    private static List<int[,]> mapLevels = new List<int[,]>();

    // 各マップレベルに対応するプレイヤーの開始座標 (マップの配列インデックス x, z)
    // (注: これらの座標は、あなたのゲームに合わせて調整する必要があります)
    private static List<Vector2Int> levelStartPositions = new List<Vector2Int>()
    {
        new Vector2Int(4, 4),  // Index 0 (level1) の開始位置
        new Vector2Int(13, 2), // Index 1 (level2) の開始位置 (level1の階段(13,14)と対にするなら (14,13) 付近)
        new Vector2Int(2, 13), // Index 2 (level3) の開始位置
        new Vector2Int(1, 1),  // Index 3 (level4) の開始位置
        new Vector2Int(1, 1)   // Index 4 (level5) の開始位置
    };

    // 生成したマップオブジェクト（壁、床など）をまとめる親オブジェクト
    private GameObject mapHolder;

    // シングルトンパターンの実装
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return; // 重複インスタンスは以降の処理を行わない
        }

        // マップデータをリストに登録 (初回起動時のみ)
        if (mapLevels.Count == 0)
        {
            mapLevels.Add(level1);
            mapLevels.Add(level2);
            mapLevels.Add(level3);
            mapLevels.Add(level4);
            mapLevels.Add(level5);
        }
    }

    void Start()
    {
        // 戦闘から復帰した直後（IsReturningFromBattle が true）の場合
        if (GameManager.instance != null && GameManager.instance.IsReturningFromBattle())
        {
            Debug.Log("戦闘から復帰。MapGeneratorのStart処理をスキップします。");
            // (この場合、GameManager.RestoreGameStateAfterLoad がマップを復元します)
            return;
        }

        // 通常起動時（戦闘復帰でない場合）
        int levelToLoad = 0; // デフォルトはレベル1 (Index 0)

        // GameManager が存在し、有効なレベルインデックスを保持しているかチェック
        if (GameManager.instance != null &&
            GameManager.instance.currentMapLevelIndex >= 0 &&
            GameManager.instance.currentMapLevelIndex < mapLevels.Count)
        {
            // (例: DeathSceneから戻ってきた場合や、StartSceneから来た場合)
            // GameManagerが記憶している階層インデックスを使用する
            levelToLoad = GameManager.instance.currentMapLevelIndex;
            Debug.Log($"GameManagerから階層インデックス {levelToLoad} を引き継いでマップを生成します。");
        }
        else
        {
            Debug.Log($"GameManagerがいないかインデックスが無効なため、デフォルトの階層 (Index {levelToLoad}) で開始します。");
        }

        // 決定した階層(levelToLoad)で、敵あり(true)でマップを生成します。
        ChangeMap(levelToLoad, true);
    }

    // 外部からマップ変更を要求する際の簡易メソッド（インデックス指定・敵あり）
    public void ChangeMap(int levelIndex)
    {
        ChangeMap(levelIndex, true); // 敵を生成する(true)バージョンを呼び出す
    }

    // マップデータを指定された「インデックス」に変更し、マップ全体を再生成します。
    // (PlayerControllerなどで階段を踏んだ時に呼び出すことを想定)
    public void ChangeMap(int levelIndex, bool generateEnemies)
    {
        // 1. 無効なインデックスかチェック
        if (levelIndex < 0 || levelIndex >= mapLevels.Count)
        {
            Debug.LogError($"無効なマップレベルインデックス {levelIndex} が指定されました。");
            return;
        }

        // 2. 古いマップオブジェクトを破棄
        if (mapHolder != null)
        {
            Destroy(mapHolder);
        }
        GameObject restoredEnemiesHolder = GameObject.Find("Restored_Enemies");
        if (restoredEnemiesHolder != null)
        {
            Destroy(restoredEnemiesHolder);
        }

        // 3. 管理リストをリセット
        chestObjectLists.Clear();
        openWallObjectLists.Clear();

        // 4. 新しいマップデータをセット
        map = (int[,])mapLevels[levelIndex].Clone();

        // 5. GameManagerの探索済みデータをリセット
        if (GameManager.instance != null)
        {
            // GameManagerに現在のレベルインデックスを記録
            GameManager.instance.currentMapLevelIndex = levelIndex;

            if (!GameManager.instance.IsReturningFromBattle())
            {
                GameManager.instance.ResetExplorationData();
            }
        }

        // 6. 新しいマップデータを基に、マップ生成処理を呼び出す
        GenerateMap(generateEnemies);

        // 7. 【追加】プレイヤーの初期位置を設定する
        //    (戦闘からの復帰時は、GameManagerがRestoreGameStateAfterLoadで
        //     位置を復元するため、この処理はスキップする)
        if (GameManager.instance == null || !GameManager.instance.IsReturningFromBattle())
        {
            SetPlayerStartPosition(levelIndex);
        }
    }

    // マップデータを指定されたものに変更し、マップ全体を再生成します。
    // (注: この int[,] 版は、主に GameManager.RestoreGameStateAfterLoad による戦闘復帰処理のために残されています)
    public void ChangeMap(int[,] newMap, bool generateEnemies)
    {
        // 1. 古いマップオブジェクト（Map Holder）が存在すれば破棄します。
        if (mapHolder != null)
        {
            Destroy(mapHolder);
        }

        // 2. GameManagerが復元した敵（"Restored_Enemies"）も、いれば破棄します。
        GameObject restoredEnemiesHolder = GameObject.Find("Restored_Enemies");
        if (restoredEnemiesHolder != null)
        {
            Destroy(restoredEnemiesHolder);
        }

        // 3. 宝箱や開く壁の管理リスト（辞書）をリセットします。
        chestObjectLists.Clear();
        openWallObjectLists.Clear();

        // 4. 新しいマップデータをセットします。
        map = (int[,])newMap.Clone();

        // 3.5 新しいマップになるため、GameManagerの探索済みデータをリセットします。
        if (GameManager.instance != null)
        {
            if (!GameManager.instance.IsReturningFromBattle())
            {
                GameManager.instance.ResetExplorationData();
            }
        }

        // 4. 新しいマップデータを基に、マップ生成処理を呼び出します。
        GenerateMap(generateEnemies);

        // (注: このバージョンでは SetPlayerStartPosition は呼ばない。
        //  戦闘復帰時は GameManager が位置を復元するため)
    }


    // マップデータ(map)に基づいて、シーンにゲームオブジェクトを配置します。
    void GenerateMap(bool generateEnemies)
    {
        // 生成するオブジェクト群をまとめるための親オブジェクトを作成
        mapHolder = new GameObject("Map Holder");

        // マップ生成開始時に、このマップの宝箱のカウントと管理リストを初期化します。
        totalChestsInCurrentMap = 0;
        openedChestsInCurrentMap = 0;
        openWallObjectLists.Clear(); // (ChangeMapでも実行しているが、念のため)

        // --- マップデータに基づいてオブジェクトを配置 (Y=1の層) ---
        // (x, z) = (0, 0) から (15, 15) までループ
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                // マップ座標(0～15)をワールド座標(-7.5f～7.5f)に変換
                float posX = x - 7.5f;
                float posZ = z - 7.5f;

                // マップデータの値(map[z, x])に応じて処理を分岐

                // 1: 壁
                if (map[z, x] == 1)
                {
                    Vector3 wallPosition = new Vector3(posX, 1f, posZ);
                    Instantiate(wallPrefab, wallPosition, Quaternion.identity, mapHolder.transform);
                }
                // 3: 宝箱
                else if (map[z, x] == 3)
                {
                    // このマップの宝箱総数をカウントアップ
                    totalChestsInCurrentMap++;

                    // 宝箱管理用の辞書キー（座標）
                    Vector2Int key = new Vector2Int(x, z);
                    // もし辞書にこの座標が未登録なら、新しいリストを作成して登録
                    if (!chestObjectLists.ContainsKey(key))
                    {
                        chestObjectLists[key] = new List<GameObject>();
                    }

                    // 宝箱をY=4.5fの位置に生成
                    Vector3 chestPosition = new Vector3(posX, 4.5f, posZ);
                    GameObject chestInstance = Instantiate(ChestPrefab, chestPosition, Quaternion.identity, mapHolder.transform);

                    // 生成した宝箱オブジェクトを、座標(key)に紐づけて辞書に追加
                    chestObjectLists[key].Add(chestInstance);
                }
                // 4: 敵
                else if (map[z, x] == 4 && generateEnemies) // generateEnemiesフラグがtrueの場合のみ
                {
                    Vector3 enemyPosition = new Vector3(posX, 0.5f, posZ);
                    Instantiate(EnemyPrefab, enemyPosition, Quaternion.identity, mapHolder.transform);
                }
                // 5: 開く壁
                else if (map[z, x] == 5)
                {
                    Vector2Int key = new Vector2Int(x, z);
                    if (!openWallObjectLists.ContainsKey(key))
                    {
                        openWallObjectLists[key] = new List<GameObject>();
                    }

                    // 通常の壁と同じY=1fの位置に「開く壁」プレハブを生成
                    Vector3 wallPosition = new Vector3(posX, 1f, posZ);
                    GameObject openWallInstance = Instantiate(openWallPrefab, wallPosition, Quaternion.identity, mapHolder.transform);

                    // 生成した「開く壁」を辞書に追加（後で削除するため）
                    openWallObjectLists[key].Add(openWallInstance);
                }
            }
        }

        // --- 高さ0の床と階段を生成 ---
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                float posX = x - 7.5f;
                float posZ = z - 7.5f;
                Vector3 floorPosition = new Vector3(posX, 0f, posZ);
                Vector3 stairsPosition = new Vector3(posX, -0.55f, posZ); // 階段用の位置調整

                // 2: 階段
                if (map[z, x] == 2)
                {
                    Instantiate(StairsPrefab, stairsPosition, Quaternion.identity, mapHolder.transform);
                }
                // それ以外: 床
                else
                {
                    Instantiate(floorPrefab, floorPosition, Quaternion.identity, mapHolder.transform);
                }
            }
        }

        // --- 高さ2の天井（または上階の床）を生成 ---
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                float posX = x - 7.5f;
                float posZ = z - 7.5f;
                Vector3 ceilingPosition = new Vector3(posX, 2f, posZ);
                Instantiate(floorPrefab, ceilingPosition, Quaternion.identity, mapHolder.transform);
            }
        }

        // --- 高さ5の床/天井と、見えない階段を生成 ---
        for (int z = 0; z < 16; z++)
        {
            for (int x = 0; x < 16; x++)
            {
                float posX = x - 7.5f;
                float posZ = z - 7.5f;
                Vector3 upperFloorPosition = new Vector3(posX, 5f, posZ);
                Vector3 hiddenStairsPosition = new Vector3(posX, -4.55f, posZ); // 見えない位置

                // 2: 階段 (見えない位置に階段を配置)
                if (map[z, x] == 2)
                {
                    Instantiate(StairsPrefab, hiddenStairsPosition, Quaternion.identity, mapHolder.transform);
                }
                // それ以外: 上層階の床 (wallPrefabView)
                else
                {
                    Instantiate(wallPrefabView, upperFloorPosition, Quaternion.identity, mapHolder.transform);
                }
            }
        }
    }

    // 【追加】指定されたレベルインデックスに対応する初期位置にプレイヤーを移動させます。
    private void SetPlayerStartPosition(int levelIndex)
    {
        if (levelIndex < 0 || levelIndex >= levelStartPositions.Count)
        {
            Debug.LogError($"レベル {levelIndex} の開始位置が未定義です。levelStartPositionsリストを確認してください。");
            return;
        }

        // 1. マップ座標 (x, z) を取得
        Vector2Int startPos = levelStartPositions[levelIndex];

        // 2. ワールド座標に変換 (GenerateMap の計算式と合わせる)
        float posX = startPos.x - 7.5f;
        float posZ = startPos.y - 7.5f; // (注: Vector2Int.y は Z座標に対応)

        // (Y座標はプレイヤーの基準位置に合わせる。
        //  EnemyPrefabが 0.5f で生成されているため、プレイヤーも 0.5f と仮定)
        Vector3 worldPosition = new Vector3(posX, 1.0f, posZ);

        // 3. プレイヤーオブジェクトを検索
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // 4. CharacterControllerを一時的に無効化して位置を強制設定
            // (GameManager.RestoreGameStateAfterLoad と同じ方式)
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = worldPosition;

            if (cc != null) cc.enabled = true;

            Debug.Log($"プレイヤーの開始位置を レベル {levelIndex} の {worldPosition} (マップ座標 {startPos.x}, {startPos.y}) に設定しました。");
        }
        else
        {
            Debug.LogWarning("SetPlayerStartPosition: プレイヤーが見つかりませんでした。");
        }
    }


    // 指定された座標(x, z)にある宝箱オブジェクトをすべて削除します（＝宝箱を開ける処理）
    public void RemoveChestObjectsAt(int x, int z)
    {
        Vector2Int key = new Vector2Int(x, z);

        // 辞書にその座標(key)が登録されているか確認
        if (chestObjectLists.ContainsKey(key))
        {
            // その座標にある宝箱オブジェクト（のリスト）をすべてループ処理で破壊(Destroy)する
            foreach (GameObject chest in chestObjectLists[key])
            {
                if (chest != null)
                {
                    Destroy(chest);
                }
            }

            // 処理が完了したので、辞書からその座標(key)の情報を削除する
            chestObjectLists.Remove(key);

            // 開けた宝箱の数をカウントアップ
            openedChestsInCurrentMap++;
            Debug.Log($"宝箱を開けました。 ({openedChestsInCurrentMap} / {totalChestsInCurrentMap})");

            // すべての宝箱を開けたかどうかをチェックする
            CheckAllChestsOpened();
        }
    }

    // マップ上のすべての宝箱が開けられたかチェックし、条件を満たしていれば「開く壁(openWall)」を削除します。
    private void CheckAllChestsOpened()
    {
        // 1. マップに宝箱が1つ以上存在し (totalChestsInCurrentMap > 0)
        // 2. 開けた宝箱の数(openedChestsInCurrentMap)が総数以上になった場合
        if (totalChestsInCurrentMap > 0 && openedChestsInCurrentMap >= totalChestsInCurrentMap)
        {
            Debug.Log("すべての宝箱を開けました！ openWall を削除します。");

            // openWallObjectLists（開く壁の管理辞書）に登録されているすべての壁を処理
            foreach (var entry in openWallObjectLists)
            {
                Vector2Int key = entry.Key; // 座標(x, z)
                List<GameObject> walls = entry.Value; // その座標にある壁オブジェクトのリスト

                // リスト内の壁オブジェクトをすべて破壊
                foreach (GameObject wall in walls)
                {
                    if (wall != null)
                    {
                        Destroy(wall);
                    }
                }

                // マップデータ上も「開く壁」(5) から「道」(0) に変更
                if (map[key.y, key.x] == 5)
                {
                    map[key.y, key.x] = 0;
                }
            }

            // すべての「開く壁」を削除したので、管理リストをクリアします。
            openWallObjectLists.Clear();
        }
    }
}