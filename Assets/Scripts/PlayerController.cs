// PlayerController.cs (入力ロジック分離 修正版)

using UnityEngine;
using System.Collections;
using System.Collections.Generic; // GetValidCombatDirections で List を使用
using System.Linq; // (このスクリプトでは Linq は使用されていませんが、念のため残します)
using UnityEngine.SceneManagement;

// [ここから追加] UDP受信に必要なライブラリ
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Concurrent;
using System.Text; // Encoding.UTF8
// [追加ここまで]

// (中略: クラス冒頭、Start, 武器関連メソッド, Update など)
// プレイヤーキャラクターの操作（移動、武器の切り替え、探索）や、
// 敵との遭遇、アイテム取得などのインタラクションを処理します。
public class PlayerController : MonoBehaviour
{
    [Header("Roto Objects (Weapon 1)")]
    public GameObject roto1; // A (Z+)
    public GameObject roto2; // D (Z-)
    public GameObject roto3; // S (X-)
    public GameObject roto4; // W (X+)

    [Header("Rod Objects (Weapon 2)")]
    [Tooltip("インスペクタからMainScene用のRodオブジェクトを設定してください")]
    public GameObject rod1; // A (Z+)
    public GameObject rod2; // D (Z-)
    public GameObject rod3; // S (X-)
    public GameObject rod4; // W (X+)

    [Header("Tanni Objects (Item Get FX)")]
    public GameObject tanni1; // A (Z+)
    public GameObject tanni2; // D (Z-)
    public GameObject tanni3; // S (X-)
    public GameObject tanni4; // W (X+)

    // 宝箱取得時のエフェクト（Tanni）表示用コルーチン
    private Coroutine showTanniCoroutine;
    // 自身のステータスコンポーネント
    private CharacterStats myStats;


    // --- [ここから追加] UDP受信用の内部変数 ---
    [Header("UDP 受信設定")]
    [Tooltip("Python (cap.py) 側で設定したポートと合わせる")]
    public int udpListenPort = 12345;

    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isThreadRunning = false;
    // スレッドセーフなキュー (サブスレッド -> メインスレッド(Update) へのデータ受け渡し用)
    private ConcurrentQueue<string> receivedDataQueue = new ConcurrentQueue<string>();

    // 受信したキー入力状態を保持する変数 (このフレームで押されたか)
    private bool isW_Pressed = false;
    private bool isA_Pressed = false;
    private bool isS_Pressed = false;
    private bool isD_Pressed = false;
    private bool isSpace_Pressed = false;
    // [追加ここまで] ---


    void Start()
    {
        if (transform.position != Vector3.zero)
        {
            UpdateExploration(transform.position);
        }
        HideAllWeapons();
        tanni1.SetActive(false);
        tanni2.SetActive(false);
        tanni3.SetActive(false);
        tanni4.SetActive(false);

        // 自身の CharacterStats を取得
        myStats = GetComponent<CharacterStats>();
        if (myStats == null)
        {
            Debug.LogError("Player (MainScene) に CharacterStats がアタッチされていません！");
        }

        // GameManagerに自身のステータスを登録（まだ登録されていない場合）
        if (GameManager.instance != null && myStats != null)
        {
            if (!GameManager.instance.IsPlayerStatsInitialized())
            {
                // 1. (初回起動時) GameManagerにステータスを登録
                GameManager.instance.RegisterPlayerStats(myStats);
            }
            else
            {
                // 2. (戦闘復帰時 や DeathSceneからの復帰時)
                //    GameManagerに保存されているステータス（HPなど）を
                //    このシーンの CharacterStats (myStats) に反映（復元）する
                myStats.InitializeStats(
                    GameManager.instance.playerMaxHealth,
                    GameManager.instance.playerCurrentHealth,
                    GameManager.instance.playerAttack,
                    GameManager.instance.playerDefense,
                    GameManager.instance.playerSpeed
                );
                Debug.Log($"GameManagerからステータスを復元しました。HP: {myStats.currentHealth}/{myStats.maxHealth}");
            }
        }

        // GameManagerの状態に基づいて、武器の表示を初期化します。
        if (GameManager.instance != null)
        {
            SetActiveWeaponDisplay(GameManager.instance.currentWeaponDirectionIndex, false);
        }
        else
        {
            SetActiveWeaponDisplay(0, true);
        }

        // --- [追加] UDP受信スレッドを開始 ---
        StartUDPListener();
        // --- [追加ここまで] ---
    }

    // [追加] アプリケーション終了時にスレッドを停止
    void OnDestroy()
    {
        StopUDPListener();
    }
    // [追加] (Unityエディタの再生停止時にも呼ばれるように)
    void OnApplicationQuit()
    {
        StopUDPListener();
    }


    // --- 武器表示関連のメソッド ---

    // すべての武器モデル（Roto 1-4, Rod 1-4）を非表示にします。
    void HideAllWeapons()
    {
        if (roto1 != null) roto1.SetActive(false);
        if (roto2 != null) roto2.SetActive(false);
        if (roto3 != null) roto3.SetActive(false);
        if (roto4 != null) roto4.SetActive(false);

        if (rod1 != null) rod1.SetActive(false);
        if (rod2 != null) rod2.SetActive(false);
        if (rod3 != null) rod3.SetActive(false);
        if (rod4 != null) rod4.SetActive(false);
    }

    // 指定された向きの武器（RotoまたはRod）"だけ"をアクティブにします。
    // index: 武器の向き (0=A/Z+, 1=D/Z-, 2=S/X-, 3=W/X+)
    // updateGameManager: GameManagerに保持されている向き(currentWeaponDirectionIndex)も更新するかどうか
    void SetActiveWeaponDisplay(int index, bool updateGameManager)
    {
        if (GameManager.instance == null) return; // GameManagerがなければ処理中断

        // 1. (必要なら) GameManagerの「現在の向き」の値を更新
        if (updateGameManager)
        {
            GameManager.instance.currentWeaponDirectionIndex = index;
        }

        // 2. GameManagerから現在の武器状態（RotoかRodか、向きはどれか）を読み取る
        bool isRoto = GameManager.instance.isRotoActive;
        int directionIndex = GameManager.instance.currentWeaponDirectionIndex;

        // 3. すべての武器モデルの表示/非表示を更新
        //    (例: isRotoがtrue かつ directionIndexが0 の場合のみ roto1 を表示)

        // Roto
        // [0] = roto1 (Aキー対応) -> Z+
        // [1] = roto2 (Dキー対応) -> Z-
        // [2] = roto3 (Sキー対応) -> X-
        // [3] = roto4 (Wキー対応) -> X+
        if (roto1 != null) roto1.SetActive(isRoto && (directionIndex == 0)); // A (Z+)
        if (roto2 != null) roto2.SetActive(isRoto && (directionIndex == 1)); // D (Z-)
        if (roto3 != null) roto3.SetActive(isRoto && (directionIndex == 2)); // S (X-)
        if (roto4 != null) roto4.SetActive(isRoto && (directionIndex == 3)); // W (X+)

        // Rod
        if (rod1 != null) rod1.SetActive(!isRoto && (directionIndex == 0)); // A (Z+)
        if (rod2 != null) rod2.SetActive(!isRoto && (directionIndex == 1)); // D (Z-)
        if (rod3 != null) rod3.SetActive(!isRoto && (directionIndex == 2)); // S (X-)
        if (rod4 != null) rod4.SetActive(!isRoto && (directionIndex == 3)); // W (X+)
    }

    // 武器の種類（Roto ⇔ Rod）を切り替えます。
    void ToggleWeaponType()
    {
        if (GameManager.instance == null) return;

        // 1. GameManagerの状態(isRotoActive)を反転させる
        GameManager.instance.isRotoActive = !GameManager.instance.isRotoActive;

        // 2. 表示を更新
        //    (GameManagerの現在の向き(currentWeaponDirectionIndex)は変更せず、そのまま表示を更新)
        SetActiveWeaponDisplay(GameManager.instance.currentWeaponDirectionIndex, false);

        Debug.Log(GameManager.instance.isRotoActive ? "武器を Roto に切り替えました。" : "武器を Rod に切り替えました。");
    }

    // --- Update (入力処理) ---

    void Update()
    {
        // --- UDP受信データの処理 ---

        // 1. このフレームのキー状態をリセット
        isW_Pressed = false;
        isA_Pressed = false;
        isS_Pressed = false;
        isD_Pressed = false;
        isSpace_Pressed = false; // 毎フレームリセット

        string latestDirectionData = null; // 向きの判定用に、最新のメッセージを保持
        bool spaceFoundInQueue = false;    // このフレームでSPACEを1回でも受信したか

        // 2. キューに溜まっているデータをすべて処理
        while (receivedDataQueue.TryDequeue(out string data))
        {
            latestDirectionData = data; // 向きは常に最新のデータで上書き

            string[] parts = data.Split(',');
            if (parts.Length >= 2)
            {
                string action = parts[1];

                // キューの中の *どれか1つでも* "SPACE" を含んでいたらフラグを立てる
                if (action == "SPACE")
                {
                    spaceFoundInQueue = true;
                }
            }
        }

        // 3. 向きの判定 (最新のデータに基づいて行う)
        if (latestDirectionData != null)
        {
            string[] parts = latestDirectionData.Split(',');
            if (parts.Length >= 1) // 向きデータ (parts[0]) があるか
            {
                string direction = parts[0];
                if (direction == "W") isW_Pressed = true;
                else if (direction == "A") isA_Pressed = true;
                else if (direction == "S") isS_Pressed = true;
                else if (direction == "D") isD_Pressed = true;
            }
        }

        // 4. 歩行の判定 (このフレームで1回でも受信していたら True にする)
        if (spaceFoundInQueue)
        {
            isSpace_Pressed = true;
        }
        // --- [UDP受信処理 ここまで] ---


        Vector3 moveDirection = Vector3.zero; // このフレームでの移動方向
        bool keyPressed = false; // 「移動」キー（SPACE）が押されたか


        // --- [ここからロジック修正] ---

        // 1. 武器の「向き」変更 (WASDキー または UDP)
        // (これは if/else if のままで良い。複数の向きを同時に押せないため)
        if (isW_Pressed || Input.GetKeyDown(KeyCode.W))
        {
            SetActiveWeaponDisplay(3, true);
        }
        else if (isS_Pressed || Input.GetKeyDown(KeyCode.S))
        {
            SetActiveWeaponDisplay(2, true);
        }
        else if (isA_Pressed || Input.GetKeyDown(KeyCode.A))
        {
            SetActiveWeaponDisplay(0, true);
        }
        else if (isD_Pressed || Input.GetKeyDown(KeyCode.D))
        {
            SetActiveWeaponDisplay(1, true);
        }
        // [注意] Cキー（武器切り替え）は、向き変更とも移動とも排他（else if）にする
        if (Input.GetKeyDown(KeyCode.C))
        {
            ToggleWeaponType();
            // Cキーはターン消費しない
            keyPressed = false;
        }

        // 2. 「移動」 (SPACEキー または UDP)
        // [★修正★] 向き変更の if ブロックとは *独立* させる。
        // (ただし、Cキーが押された場合は移動しないようにする)
        if (!Input.GetKeyDown(KeyCode.C) && (isSpace_Pressed || Input.GetKeyDown(KeyCode.Space)))
        {
            if (GameManager.instance != null)
            {
                // GameManagerから現在の向きを取得
                int directionIndex = GameManager.instance.currentWeaponDirectionIndex;

                // 向きインデックス(0=A, 1=D, 2=S, 3=W)に応じて移動方向を決定
                switch (directionIndex)
                {
                    case 0: // A (Z+)
                        moveDirection.z = 1;
                        break;
                    case 1: // D (Z-)
                        moveDirection.z = -1;
                        break;
                    case 2: // S (X-)
                        moveDirection.x = -1;
                        break;
                    case 3: // W (X+)
                        moveDirection.x = 1;
                        break;
                }

                // 移動キーが押された（＝ターン消費）
                keyPressed = true;
            }
        }

        // --- [ロジック修正 ここまで] ---


        // ターン制処理の基点となる、プレイヤーの「移動前の位置」を記録
        Vector3 playerPosBeforeMove = transform.position;

        // 3. プレイヤーの移動処理（TryMove）を実行 (SPACEが押された場合のみ)
        if (moveDirection != Vector3.zero)
        {
            TryMove(moveDirection);
        }

        // 4. 移動キー（SPACE）が押されていた場合のみ、敵のターンを実行
        if (keyPressed)
        {
            // シーン上のすべての "EnemyController" を検索
            EnemyController[] allEnemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);

            // 各敵の ExecuteTurn を呼び出し、プレイヤーの移動前位置を渡す
            foreach (EnemyController enemy in allEnemies)
            {
                enemy.ExecuteTurn(playerPosBeforeMove);
            }
        }
    }

    // --- 移動とインタラクション ---

    // 指定された方向(direction)への移動を試みます。
    // 移動先のマスのタイプに応じて、移動、戦闘突入、階段移動、アイテム取得などを処理します。
    void TryMove(Vector3 direction)
    {
        CharacterController cc = GetComponent<CharacterController>();
        Vector3 targetPosition = transform.position + direction; // 移動先の目標座標

        // --- 1. 敵との接触判定 (最優先) ---
        // OverlapSphereで移動先にコライダーがあるか簡易的にチェック
        Collider[] hitColliders = Physics.OverlapSphere(targetPosition, 0.4f);
        foreach (var hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Enemy"))
            {
                // 敵に接触した場合

                // まずは敵のいるマスへ強制的に移動（ワープ）
                if (cc != null) cc.enabled = false;
                transform.position = targetPosition;
                if (cc != null) cc.enabled = true;

                // 移動先を探索済みにする
                UpdateExploration(targetPosition);

                // このフレームで戦闘が開始したことをGameManagerに通知
                if (GameManager.instance != null)
                {
                    GameManager.instance.combatInitiatedThisFrame = true;
                }

                // 戦闘開始処理（GameManagerに委任）
                List<int> validDirections = GetValidCombatDirections(transform.position);
                GameManager.instance.PlayerCaughtByEnemy(hitCollider.gameObject, validDirections);

                return; // 敵に遭遇したら、以下の処理は行わない
            }
        }

        // --- 2. 通常の移動処理（壁・アイテムなど） ---

        // 移動先のマップ配列インデックスを計算
        int mapX = Mathf.RoundToInt(targetPosition.x + 7.5f);
        int mapZ = Mathf.RoundToInt(targetPosition.z + 7.5f);

        // マップ範囲外チェック
        if (mapX < 0 || mapX >= 16 || mapZ < 0 || mapZ >= 16)
        {
            UpdateExploration(transform.position); // 現在地（移動前）だけ探索済みにする
            return; // 移動しない
        }

        // 移動先のタイルタイプを取得
        int targetCellType = MapGenerator.map[mapZ, mapX];

        // 0: 道, 4: 敵（移動は可能）
        if (targetCellType == 0 || targetCellType == 4)
        {
            // CharacterControllerを無効化して座標を直接設定（ワープ移動）
            if (cc != null) cc.enabled = false;
            transform.position = targetPosition;
            if (cc != null) cc.enabled = true;

            // 移動先を探索済みにする
            UpdateExploration(targetPosition);
        }
        // 2: 階段
        else if (targetCellType == 2)
        {

            // 1. 現在のレベルインデックスと、踏んだ階段の座標を取得
            int currentLevel = (GameManager.instance != null) ? GameManager.instance.currentMapLevelIndex : 0;
            Vector2Int stairCoord = new Vector2Int(mapX, mapZ);

            // 2. 次に移動すべきレベルのインデックスを決定する (新設メソッド)
            int nextLevelIndex = GetNextLevelIndex(currentLevel, stairCoord);

            // 3. 取得したインデックスに基づいて処理を分岐

            // [ここから変更]
            // 3a. クリア判定 (nextLevelIndex が -2 の場合)
            if (nextLevelIndex == -2)
            {
                Debug.Log("最終レベルをクリアしました！ VictoryScene に遷移します。");

                // --- [追加] FlagManagerで階層クリアフラグを更新 ---
                if (FlagManager.instance != null)
                {
                    FlagManager.instance.NotifyFloorCleared();
                    Debug.Log("API: dungeon_floors_cleared (最終クリア)");
                }
                // ------------------------------------------------

                // (注意: "VictoryScene" が Build Settings に追加されている必要があります)
                SceneManager.LoadScene("VictoryScene");
                return; // シーン遷移するので以降の処理は不要
            }
            // 3b. 通常の階層移動 (nextLevelIndex が 0 以上の場合)
            else if (nextLevelIndex >= 0)
            {
                // インデックスでマップ切り替えを依頼
                MapGenerator.instance.ChangeMap(nextLevelIndex);

                // 4. 移動先を探索済みにする
                // (MapGenerator.ChangeMap がプレイヤー位置を自動設定するので、
                //  移動後の現在地 (transform.position) を探索済みにする)
                UpdateExploration(transform.position);

                // --- 5. HPを全回復する
                if (myStats != null && GameManager.instance != null && GameManager.instance.IsPlayerStatsInitialized())
                {
                    // プレイヤーコンポーネント(myStats)のHPを最大値にする
                    myStats.currentHealth = myStats.maxHealth;

                    // GameManagerに保存されているHPも最大値に更新する
                    // (これにより、次に戦闘に入った時に全快状態で始まる)
                    GameManager.instance.playerCurrentHealth = myStats.maxHealth;

                    Debug.Log($"階層移動によりHPが全回復しました: {myStats.currentHealth}/{myStats.maxHealth}");
                }

                // --- [追加] FlagManagerで階層クリアフラグを更新 ---
                if (FlagManager.instance != null)
                {
                    FlagManager.instance.NotifyFloorCleared();
                    Debug.Log("API: dungeon_floors_cleared (階層移動)");
                }
                // ------------------------------------------------
            }
            // 3c. 移動不可 (nextLevelIndex が -1 の場合)
            else
            {
                // 移動先が定義されていない階段だった場合（壁と同じ扱い）
                Debug.LogWarning($"階段 ({stairCoord.x}, {stairCoord.y}) に対応する移動先が未定義です。");
                UpdateExploration(transform.position); // 移動前の位置を探索済みにする
            }
            // [ここまで変更]
        }
        // 3: 宝箱
        else if (targetCellType == 3)
        {
            // 宝箱のマスへ移動
            if (cc != null) cc.enabled = false;
            transform.position = targetPosition;
            if (cc != null) cc.enabled = true;

            // 移動先を探索済みにする
            UpdateExploration(targetPosition);

            // 宝箱があったマスを「道」(0) に変更
            MapGenerator.map[mapZ, mapX] = 0;
            // MapGeneratorに宝箱オブジェクトの削除を依頼
            MapGenerator.instance.RemoveChestObjectsAt(mapX, mapZ);

            // 取得エフェクト（Tanni）の表示
            GameObject tanniToShow = null;
            if (direction.x > 0) { tanniToShow = tanni4; }      // W
            else if (direction.x < 0) { tanniToShow = tanni3; } // S
            else if (direction.z > 0) { tanniToShow = tanni1; } // A
            else if (direction.z < 0) { tanniToShow = tanni2; } // D

            if (tanniToShow != null)
            {
                // 既に表示中のエフェクトがあれば停止
                if (showTanniCoroutine != null)
                {
                    StopCoroutine(showTanniCoroutine);
                }
                // エフェクトを3秒間表示するコルーチンを開始
                showTanniCoroutine = StartCoroutine(ShowTanniAndHide(tanniToShow, 3f));
            }
        }
        // 1: 壁, 5: 開く壁 (＝移動不可)
        else
        {
            // 壁にぶつかった場合、移動はしない
            // 現在地（移動前）の探索データのみ更新
            UpdateExploration(transform.position);
        }
    }

    // --- 探索とユーティリティ ---

    // プレイヤーの現在地(currentPos)とその周囲8マスを「探索済み」としてGameManagerに記録します。
    void UpdateExploration(Vector3 currentPos)
    {
        if (GameManager.instance == null || GameManager.instance.exploredMapData == null)
        {
            return; // GameManagerまたは探索データが利用不可
        }

        // ワールド座標からマップ配列のインデックスに変換
        int centerX = Mathf.RoundToInt(currentPos.x + 7.5f);
        int centerZ = Mathf.RoundToInt(currentPos.z + 7.5f);

        // 中心(centerX, centerZ)とその周囲（-1 から +1）をループ
        for (int z = centerZ - 1; z <= centerZ + 1; z++)
        {
            for (int x = centerX - 1; x <= centerX + 1; x++)
            {
                // マップの範囲内(0～15)かチェック
                if (x >= 0 && x < 16 && z >= 0 && z < 16)
                {
                    // 探索済みフラグ(true)を立てる
                    GameManager.instance.exploredMapData[z, x] = true;
                }
            }
        }
        // (ミニマップ(MiniMapDisplay)は自らUpdateでGameManagerを参照して再描画します)
    }

    // 宝箱取得エフェクト（Tanni）を指定時間(duration)だけ表示して隠すコルーチンです。
    private IEnumerator ShowTanniAndHide(GameObject tanniObject, float duration)
    {
        // 念のため、他のエフェクトをすべて非表示に
        tanni1.SetActive(false); tanni2.SetActive(false); tanni3.SetActive(false); tanni4.SetActive(false);

        // 対象のエフェクトを表示
        tanniObject.SetActive(true);

        // 指定時間（秒）待機
        yield return new WaitForSeconds(duration);

        // エフェクトを非表示
        tanniObject.SetActive(false);
    }

    // 現在のレベル(currentLevelIndex)と踏んだ階段の座標(stairCoord)に基づいて、
    // 次に移動すべきマップの「インデックス」を返します。
    // (注: このメソッドは、あなたのゲームのマップ接続設計に合わせてカスタマイズする必要があります)
    private int GetNextLevelIndex(int currentLevelIndex, Vector2Int stairCoord)
    {
        // --- 階層移動のルールをここに定義します ---

        // (これはあくまで「仮」の接続ルールです)
        switch (currentLevelIndex)
        {
            case 0: // 現在 Level 0 (level1) にいる場合
                // 踏んだ座標が (x=4, z=11) なら Index 1 (Level 2) へ
                if (stairCoord.x == 4 && stairCoord.y == 11) return 1;
                break;

            case 1: // 現在 Level 1 (level2) にいる場合
                // 踏んだ座標が (x=13, z=14) なら Index 2 (Level 3) へ
                // (注: MapGenerator.cs の level2[14, 13] = 2 とルールが一致しているか確認してください)
                if (stairCoord.x == 3 && stairCoord.y == 3) return 2;
                // (例: Level 0 へ戻る階段)
                // if (stairCoord.x == X && stairCoord.y == Z) return 0;
                break;

            case 2: // 現在 Level 2 (level3) にいる場合
                // 踏んだ座標が (x=13, z=14) なら Index 3 (Level 4) へ
                // (注: MapGenerator.cs の level3[14, 13] = 2 とルールが一致しているか確認してください)
                if (stairCoord.x == 9 && stairCoord.y == 3) return 3;
                // (例: Level 1 へ戻る階段)
                // if (stairCoord.x == X && stairCoord.y == Z) return 1;
                break;

            case 3: // 現在 Level 3 (level4) にいる場合
                // 踏んだ座標が (x=13, z=2) なら Index 4 (Level 5) へ
                // (注: MapGenerator.cs の level4[2, 13] = 2 とルールが一致しているか確認してください)
                if (stairCoord.x == 13 && stairCoord.y == 2) return 4;
                // (例: Level 2 へ戻る階段)
                // if (stairCoord.x == X && stairCoord.y == Z) return 2;
                break;

            case 4: // 現在 Level 4 (level5) にいる場合
                // 踏んだ座標が (x=1, z=13) なら「クリア」(-2) を返す
                // (注: MapGenerator.cs の level5[13, 1] = 2 とルールが一致しているか確認してください)
                if (stairCoord.x == 1 && stairCoord.y == 13) return -2; // [ここを変更] 0 (Level 1) ではなく -2 を返す
                // (例: Level 3 へ戻る階段)
                // if (stairCoord.x == X && stairCoord.y == Z) return 3;
                break;
        }

        // 該当するルールがない場合は -1 (移動不可) を返す
        return -1;
    }

    // プレイヤーの初期位置を設定します（ゲーム開始時、または階段移動時）。
    void SetInitialPosition(bool isFirstTime) { }

    // 物理的なトリガー（Collider）に接触した時に呼ばれます。
    // (TryMoveのOverlapSphereとは別。こちらはCharacterControllerが移動した結果として検知)
    private void OnTriggerEnter(Collider other)
    {
        // 敵("Enemy"タグ)に接触した場合
        if (other.CompareTag("Enemy"))
        {
            // 戦闘開始処理（GameManagerに委任）
            List<int> validDirections = GetValidCombatDirections(transform.position);
            GameManager.instance.PlayerCaughtByEnemy(other.gameObject, validDirections);
        }
    }

    // 戦闘突入時に、プレイヤーの周囲4マスのうち、
    // 戦闘（Roto/Rod）が可能なマス（＝移動可能なマス）の方向インデックスをリストで返します。
    private List<int> GetValidCombatDirections(Vector3 currentPos)
    {
        List<int> directions = new List<int>();
        int mapX = Mathf.RoundToInt(currentPos.x + 7.5f);
        int mapZ = Mathf.RoundToInt(currentPos.z + 7.5f);

        // 武器インデックスと方向の対応
        // [0] = Aキー対応 -> MainSceneの Z+1 方向
        // [1] = Dキー対応 -> MainSceneの Z-1 方向
        // [2] = Sキー対応 -> MainSceneの X-1 方向
        // [3] = Wキー対応 -> MainSceneの X+1 方向

        // 各方向が「有効なタイル（道、宝箱、敵マス）」かチェック
        if (IsValidTile(mapX, mapZ + 1)) directions.Add(0);     // A
        if (IsValidTile(mapX, mapZ - 1)) directions.Add(1);     // D
        if (IsValidTile(mapX - 1, mapZ)) directions.Add(2);     // S
        if (IsValidTile(mapX + 1, mapZ)) directions.Add(3);     // W

        return directions;
    }

    // 指定されたマップ座標(x, z)が、戦闘（または移動）可能なタイルか判定します。
    private bool IsValidTile(int x, int z)
    {
        // マップ範囲外は無効
        if (x < 0 || x >= 16 || z < 0 || z >= 16) { return false; }
        if (MapGenerator.map == null) return false;

        int tileType = MapGenerator.map[z, x];

        // 0(道), 3(宝箱), 4(敵) のマスは有効
        return tileType == 0 || tileType == 3 || tileType == 4;
    }


    // --- [ここから追加] UDP受信メソッド群 ---

    /// <summary>
    /// UDPリスナー（別スレッド）を開始します。
    /// </summary>
    private void StartUDPListener()
    {
        try
        {
            // udpClient = new UdpClient(udpListenPort); // 従来の方法

            // [修正] Socketオプションでポートの即時再利用(ReuseAddress)を許可する
            // これにより、シーン切り替えやリスタート時にポートが即座に解放されなくても
            // "Address already in use" エラーを回避できます。
            udpClient = new UdpClient();
            udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            // SetSocketOption の後に Bind を実行
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, udpListenPort));
            // [修正ここまで]

            isThreadRunning = true;

            receiveThread = new Thread(ReceiveUDPData);
            receiveThread.IsBackground = true; // メインスレッド終了時に自動終了
            receiveThread.Start();

            Debug.Log($"UDPリスナーを開始しました。ポート: {udpListenPort}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("UDPリスナーの開始に失敗しました: " + e.Message);
        }
    }

    /// <summary>
    /// UDPリスナー（別スレッド）を停止します。
    /// </summary>
    private void StopUDPListener()
    {
        if (!isThreadRunning) return; // 既に停止処理が呼ばれている

        isThreadRunning = false;

        // UdpClientを閉じると、Receive() でブロックされているスレッドが
        // SocketException をスローして終了します。
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }

        if (receiveThread != null && receiveThread.IsAlive)
        {
            try
            {
                // スレッドが終了するのを待つ
                receiveThread.Join();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("UDP受信スレッドの停止中にエラー: " + e.Message);
            }
        }
        receiveThread = null;

        Debug.Log("UDPリスナーを停止しました。");
    }

    /// <summary>
    /// [サブスレッドで実行] UDPデータを読み取り、キューに追加します。
    /// </summary>
    private void ReceiveUDPData()
    {
        // 任意のIPアドレスからのデータを受け入れる
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);

        while (isThreadRunning)
        {
            try
            {
                if (udpClient == null) break; // 停止処理が開始された

                // データを受信する (データが来るまでここで待機/ブロックする)
                byte[] data = udpClient.Receive(ref remoteEP);

                // 受信したバイト配列をUTF-8文字列に変換
                string message = Encoding.UTF8.GetString(data);

                // スレッドセーフなキューにデータを追加 (Update側で処理される)
                receivedDataQueue.Enqueue(message);
            }
            catch (System.Net.Sockets.SocketException)
            {
                // StopUDPListener()でClose()が呼ばれると、この例外が発生して
                // スレッドが正常に終了します。
                if (isThreadRunning)
                {
                    Debug.LogWarning("SocketException (おそらくUDP Clientが閉じられました)");
                }
            }
            catch (System.Exception e)
            {
                if (isThreadRunning)
                {
                    Debug.LogError("UDPデータ受信エラー: " + e.Message);
                }
            }
        }
        Debug.Log("UDP受信スレッドを終了します。");
    }
}