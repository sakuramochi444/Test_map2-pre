// GameManager.cs

using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// ゲーム全体の進行状態（プレイヤーや敵の位置、ステータス、マップデータなど）を
// シーン間で永続的に管理するシングルトンクラスです。
public class GameManager : MonoBehaviour
{
    // シングルトンインスタンス
    public static GameManager instance;

    // --- シーン間で保持するゲーム状態 ---
    public Vector3 playerPosition; // プレイヤーの座標
    public Quaternion playerRotation; // プレイヤーの向き
    public List<Vector3> enemyPositions = new List<Vector3>(); // 敵の座標リスト
    public int[,] mapData; // マップのレイアウトデータ
    public int currentMapLevelIndex = 0;
    public bool[,] exploredMapData; // マップの探索済みデータ

    [Header("Prefabs for Restore")]
    [Tooltip("MainSceneに戻ったときに再配置するためのEnemyプレハブを設定してください")]
    public GameObject enemyPrefabForRestore; // 復元用の敵プレハブ

    // 戦闘関連
    public List<int> validCombatDirections = new List<int>(); // 戦闘突入時の有効な方向
    public bool combatInitiatedThisFrame = false; // このフレームで戦闘が開始されたか
    private Vector3 positionOfEnemyInCombat; // 戦闘対象となった敵の座標
    private bool returnedFromBattle = false; // 戦闘シーンから戻った直後か
    private string mainSceneName = "MainScene"; // メインシーン名
    private string battleSceneName = "BattleScene"; // 戦闘シーン名
    public string deathSceneName = "DeathScene"; // ゲームオーバーシーン名

    [Header("Weapon Stats (Auto Managed)")]
    // 武器の状態
    [Tooltip("現在Rotoが選択されているか (true=Roto, false=Rod)")]
    public bool isRotoActive = true;
    [Tooltip("現在の武器の向き (0=A, 1=D, 2=S, 3=W)")]
    public int currentWeaponDirectionIndex = 0;

    [Header("Player Stats (Auto Managed)")]
    // プレイヤーのステータス
    public int playerCurrentHealth;
    public int playerMaxHealth;
    public int playerAttack;
    public int playerDefense;
    public int playerSpeed;
    private bool playerStatsInitialized = false; // ステータスがGameManagerに登録済みか

    [Header("Stats (Auto Managed)")] // (分かりやすいようヘッダーを追加)
    [Tooltip("現在のセッションでの総討伐数")]
    public int totalKillCount = 0;

    void Awake()
    {
        // シングルトンパターンの実装
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject); // シーンをまたいでも破棄されないようにする

            // 初回起動時（exploredMapDataがまだnullの場合）、探索マップを初期化する
            if (exploredMapData == null)
            {
                // マップサイズは 16x16 と仮定
                exploredMapData = new bool[16, 16];
            }

            // 討伐カウントを初期化
            totalKillCount = 0;
        }
        else
        {
            // 既にインスタンスが存在する場合は、このオブジェクトを破棄する
            Destroy(gameObject);
        }
    }

    // LateUpdateは全Update処理の後に呼ばれる
    void LateUpdate()
    {
        // 戦闘開始フラグは、それがチェックされたフレームの終わりには必ずリセットする
        combatInitiatedThisFrame = false;
    }

    // プレイヤーが敵に捕まった（戦闘に突入した）時に呼ばれます
    public void PlayerCaughtByEnemy(GameObject enemyInCombat, List<int> validDirections)
    {
        Debug.Log("敵に捕まった！戦闘シーンへ移行します。");

        // 1. メインシーンの現在の状態を保存する
        SaveGameState(enemyInCombat, validDirections);

        // 2. 戦闘シーンをロードする
        SceneManager.LoadScene(battleSceneName);
    }

    // 戦闘シーンからメインシーンへ戻る時に呼ばれます
    public void ReturnToMainScene()
    {
        Debug.Log($"メインシーンへ戻ります。プレイヤーHP: {playerCurrentHealth}");

        // 復帰フラグを立ててからメインシーンをロードする
        // (OnSceneLoaded がこのフラグを見て RestoreGameStateAfterLoad を呼び出す)
        returnedFromBattle = true;
        SceneManager.LoadScene(mainSceneName);
    }

    public void GoToDeathScene()
    {
        Debug.Log("プレイヤーが死亡しました。DeathSceneへ移行します。");
        // この時点で GameManager には戦闘突入前の MainScene の状態が
        // 保存されたままなので、DeathScene のボタンから復帰が可能です。
        returnedFromBattle = false; // MainSceneへの復元はしない
        SceneManager.LoadScene(deathSceneName);
    }

    // 戦闘突入時に、現在のゲーム状態（プレイヤー、敵、マップ）を保存します
    private void SaveGameState(GameObject enemyInCombat, List<int> validDirections)
    {
        // プレイヤーの位置・向きを保存
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerPosition = player.transform.position;
            playerRotation = player.transform.rotation;

            // プレイヤーステータスを保存
            CharacterStats playerStats = player.GetComponent<CharacterStats>();
            if (playerStats != null && playerStatsInitialized)
            {
                playerMaxHealth = playerStats.maxHealth;
                playerCurrentHealth = playerStats.currentHealth;
                playerAttack = playerStats.attack;
                playerDefense = playerStats.defense;
                playerSpeed = playerStats.speed;
                Debug.Log($"戦闘突入。プレイヤーHP {playerCurrentHealth}/{playerMaxHealth} を保存しました。");
            }
            // まだGameManagerにステータスが登録されていない場合 (初回起動時など)
            else if (playerStats != null && !playerStatsInitialized)
            {
                Debug.LogWarning("PlayerStatsがまだGameManagerに登録されていません。先に登録処理を実行します。");
                RegisterPlayerStats(playerStats);
                // ステータスを登録した後、再度保存処理を実行する
                SaveGameState(enemyInCombat, validDirections);
                return;
            }
        }

        // 戦闘対象の「敵」の位置をキャッシュ
        positionOfEnemyInCombat = enemyInCombat.transform.position;

        // 戦闘対象「以外」のすべての敵の位置をリストに保存
        enemyPositions.Clear();
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (var enemy in enemies)
        {
            // 戦闘に突入する敵自身は、復元リストから除外する
            if (enemy == enemyInCombat)
            {
                Debug.Log($"敵 {enemy.name} は戦闘対象のため、保存リストから除外します。");
                continue;
            }
            enemyPositions.Add(enemy.transform.position);
        }
        Debug.Log($"戦闘突入。{enemyPositions.Count} 体の敵の位置を保存しました。");

        // マップデータ（レイアウト）を保存
        mapData = (int[,])MapGenerator.map.Clone();

        // 探索済みマップデータを保存
        if (this.exploredMapData != null)
        {
            exploredMapData = (bool[,])this.exploredMapData.Clone();
        }
        else
        {
            // 万が一 exploredMapData が null だった場合のフォールバック
            exploredMapData = new bool[16, 16];
        }

        // 戦闘時の有効な方向を保存
        validCombatDirections.Clear();
        if (validDirections != null)
        {
            validCombatDirections.AddRange(validDirections);
        }
    }

    // PlayerControllerなどから呼び出され、プレイヤーのステータスをGameManagerに登録します
    public void RegisterPlayerStats(CharacterStats stats)
    {
        if (playerStatsInitialized) return; // 既に登録済みの場合は何もしない

        playerMaxHealth = stats.maxHealth;
        playerCurrentHealth = stats.currentHealth;
        playerAttack = stats.attack;
        playerDefense = stats.defense;
        playerSpeed = stats.speed;
        playerStatsInitialized = true; // 登録済みフラグを立てる
        Debug.Log($"プレイヤーの初期ステータスをGameManagerに登録しました。HP: {playerCurrentHealth}/{playerMaxHealth}");
    }

    // プレイヤーステータスが登録済みかを返します
    public bool IsPlayerStatsInitialized()
    {
        return playerStatsInitialized;
    }


    // メインシーンがロードされた後（戦闘からの復帰時）に、状態を復元するコルーチンです
    private IEnumerator RestoreGameStateAfterLoad()
    {
        // シーンが完全にロードされ、オブジェクトが配置されるのを1フレーム待機します
        yield return null;

        Debug.Log("ゲームの状態を復元します。");

        // プレイヤーの位置・向きを復元
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // transform.positionを直接変更するため、CharacterControllerを一時的に無効化
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            player.transform.position = playerPosition;
            player.transform.rotation = playerRotation;

            if (cc != null) cc.enabled = true; // CharacterControllerを再度有効化

            // プレイヤーのステータス（特に戦闘で変動したHP）を復元
            CharacterStats playerStats = player.GetComponent<CharacterStats>();
            if (playerStats != null && playerStatsInitialized)
            {
                playerStats.InitializeStats(
                    playerMaxHealth,
                    playerCurrentHealth,
                    playerAttack,
                    playerDefense,
                    playerSpeed
                );
                Debug.Log($"戦闘から復帰。プレイヤーHPを {playerStats.currentHealth}/{playerStats.maxHealth} に復元しました。");
            }
        }

        // マップデータを復元
        if (MapGenerator.instance != null && mapData != null)
        {
            // マップデータをMapGeneratorに渡して再生成させる (falseはアニメーションなしの意)
            MapGenerator.instance.ChangeMap(mapData, false);
        }
        else
        {
            Debug.LogError("MapGeneratorのインスタンスまたはmapDataが見つかりません。");
        }

        // 敵を保存された位置に再配置
        if (enemyPrefabForRestore != null)
        {
            GameObject enemyHolder = new GameObject("Restored_Enemies");
            foreach (Vector3 pos in enemyPositions)
            {
                // 保存しておいた位置(pos)に、指定されたプレハブ(enemyPrefabForRestore)を生成
                Instantiate(enemyPrefabForRestore, pos, Quaternion.identity, enemyHolder.transform);
            }
            Debug.Log($"{enemyPositions.Count} 体の敵を保存位置から復元しました。");
        }
        else
        {
            Debug.LogError("GameManagerに 'Enemy Prefab For Restore' が設定されていません。敵を復元できません。");
        }

        // 復元が完了したので、フラグをリセット
        returnedFromBattle = false;
    }

    // 探索済みマップデータをリセットします（例：新しいフロアに進んだ時など）
    public void ResetExplorationData()
    {
        Debug.Log("新しいマップレベルのため、探索データをリセットします。");
        exploredMapData = new bool[16, 16];
    }

    // 戦闘シーンから戻ってきた直後かどうかを外部に伝えます
    public bool IsReturningFromBattle()
    {
        return returnedFromBattle;
    }

    // --- シーンロード時のイベントハンドラ設定 ---

    // シーンがロードされた時に自動的に呼ばれるメソッド
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // もしロードされたのがメインシーンで、かつ戦闘から戻ってきたところなら
        if (scene.name == mainSceneName && returnedFromBattle)
        {
            // 状態を復元するコルーチンを開始する
            StartCoroutine(RestoreGameStateAfterLoad());
        }
    }

    /// <summary>
    /// 現在の階層（GameManagerが記憶しているレベル）を初期状態からやり直します。
    /// DeathScene やポーズメニューから呼び出されます。
    /// </summary>
    public void RestartCurrentLevel()
    {
        Debug.Log($"現在の階層 (Level {currentMapLevelIndex}) を初期状態からやり直します。");

        // 1. 復帰フラグを false にする
        //    これにより、MainSceneロード時に RestoreGameStateAfterLoad が実行されず、
        //    MapGenerator.Start() が実行されるようになります。
        returnedFromBattle = false;

        // 2. GameManager が保持しているプレイヤーのHPを最大値にリセットする
        //    (ステータスが初期化されている場合のみ)
        if (playerStatsInitialized)
        {
            playerCurrentHealth = playerMaxHealth;
            Debug.Log($"プレイヤーHPを {playerCurrentHealth}/{playerMaxHealth} にリセットしました。");
        }
        else
        {
            Debug.LogWarning("GameManagerのステータスが未初期化のため、HPリセットはスキップされました。 (MainSceneロード後に初期化されます)");
        }

        // 3. MainScene をロードする
        SceneManager.LoadScene(mainSceneName);
    }

    /// <summary>
    /// 敵を倒した時に BattleGameManager から呼び出され、総討伐数を1増やします。
    /// </summary>
    public void NotifyEnemyDefeated()
    {
        totalKillCount++;
        // (注: ここではAPI送信は行わない)
        Debug.Log($"討伐数をインクリメントしました。総討伐数: {totalKillCount}");
    }

    // オブジェクトが有効になった時に呼ばれる
    void OnEnable()
    {
        // シーンロード時のイベント(OnSceneLoaded)を購読（登録）する
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // オブジェクトが無効になった時に呼ばれる
    void OnDisable()
    {
        // 購読（登録）を解除する
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}