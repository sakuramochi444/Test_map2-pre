// BattleGameManager.cs (UDP + シリアル併用版)

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// --- [変更] UDPとシリアルの両方のライブラリをインポート ---
using System.IO.Ports; // シリアル
using System.Net;      // UDP
using System.Net.Sockets; // UDP
using System.Threading;
using System.Collections.Concurrent;
using System.Text; // Encoding.UTF8
// --- [変更ここまで] ---

[RequireComponent(typeof(AudioSource))]
public class BattleGameManager : MonoBehaviour
{
    // ... (スライム ～ rodLHealAmount までのインスペクタ設定は変更なし) ...
    [Header("スロット別モンスター (インスペクタから設定)")]
    public CharacterStats[] slimes = new CharacterStats[4];
    public CharacterStats[] skeletons = new CharacterStats[4];
    public CharacterStats[] golems = new CharacterStats[4];
    public CharacterStats[] ghosts = new CharacterStats[4];
    // ... (武器オブジェクト ～ rodLHealAmount まで変更なし) ...
    public GameObject[] rotos = new GameObject[4];
    public GameObject[] rods = new GameObject[4];
    public CharacterStats playerStats;
    public GameObject[] rotoAttackEffectPrefabs = new GameObject[4];
    public GameObject[] rodAttackEffectPrefabs_J = new GameObject[4];
    public GameObject[] rodAttackEffectPrefabs_K = new GameObject[4];
    public GameObject[] rodAttackEffectPrefabs_L = new GameObject[4];
    public AudioClip rotoAttackSound;
    public AudioClip rodAttackSound_J;
    public AudioClip rodAttackSound_K;
    public AudioClip rodAttackSound_L;
    public AudioClip ineffectiveAttackSound;
    public int rodLHealAmount = 20;


    // --- [変更] UDPとシリアルの両方の設定を追加 ---
    [Header("UDP 受信設定 (Python用)")]
    [Tooltip("Python (cap.py) 側で設定したポートと合わせる")]
    public int udpListenPort = 12345;

    // ... (currentEnemies ～ gameOverSceneName は変更なし) ...
    private CharacterStats[] currentEnemies = new CharacterStats[4];
    private float[] enemyActionCounters;
    private AudioSource audioSource;
    private Dictionary<GameObject, Coroutine> activeEffectCoroutines = new Dictionary<GameObject, Coroutine>();
    private bool isRodKAttackBoosted = false;
    private bool hasUsedRodLHeal = false;
    private bool isBattleEnding = false;
    private AudioClip lastPlayedAttackSound = null;
    public string gameOverSceneName = "MainScene";


    private FlagManager flagManager;

    // --- [変更] UDP用とシリアル用の変数を両方定義 ---

    // UDP受信関連
    private UdpClient udpClient;
    private Thread udpReceiveThread;
    private bool isUdpThreadRunning = false;
    private ConcurrentQueue<string> udpReceivedDataQueue = new ConcurrentQueue<string>();

    // 受信したキー入力状態を保持する変数 (このフレームで押されたか)
    private bool isW_Pressed = false;
    private bool isA_Pressed = false;
    private bool isS_Pressed = false;
    private bool isD_Pressed = false;
    private bool isSpace_Pressed = false; // UDPの"SPACE" または シリアルの "s"
    private bool isJ_Pressed = false; // UDP "J"
    private bool isK_Pressed = false; // UDP "K"
    private bool isL_Pressed = false; // UDP "L"
    // --- [変更ここまで] ---


    void Start()
    {
        // ... (audioSource ～ FlagManager までは変更なし) ...
        audioSource = GetComponent<AudioSource>();
        // ... (playerStats の初期化処理も変更なし) ...
        // ... (InitializeEnemies, 武器表示, エフェクト非表示なども変更なし) ...

        flagManager = FlagManager.instance;
        if (flagManager == null) { Debug.LogError("FlagManager.instance が見つかりません！"); }
        foreach (var roto in rotos) { if (roto != null) roto.SetActive(false); }
        foreach (var rod in rods) { if (rod != null) rod.SetActive(false); }
        enemyActionCounters = new float[4];
        if (playerStats == null) { Debug.LogError("BattleGameManagerの 'Player Stats' が設定されていません！"); }
        else
        {
            if (GameManager.instance != null && GameManager.instance.IsPlayerStatsInitialized())
            {
                playerStats.InitializeStats(GameManager.instance.playerMaxHealth, GameManager.instance.playerCurrentHealth, GameManager.instance.playerAttack, GameManager.instance.playerDefense, GameManager.instance.playerSpeed);
            }
            if (GameManager.instance != null)
            {
                CharacterController cc = playerStats.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                playerStats.transform.position = GameManager.instance.playerPosition;
                playerStats.transform.rotation = GameManager.instance.playerRotation;
                if (cc != null) cc.enabled = true;
            }
            playerStats.OnDied.AddListener(OnPlayerDied);
        }
        InitializeEnemies();
        if (GameManager.instance != null) { SetActiveWeaponDisplay(GameManager.instance.currentWeaponDirectionIndex, false); }
        else { SetActiveWeaponDisplay(0, true); }
        foreach (var effect in rotoAttackEffectPrefabs) { if (effect != null) effect.SetActive(false); }
        foreach (var effect in rodAttackEffectPrefabs_J) { if (effect != null) effect.SetActive(false); }
        foreach (var effect in rodAttackEffectPrefabs_K) { if (effect != null) effect.SetActive(false); }
        foreach (var effect in rodAttackEffectPrefabs_L) { if (effect != null) effect.SetActive(false); }
        isRodKAttackBoosted = false;
        hasUsedRodLHeal = false;


        // --- [変更] UDPリスナーとシリアルポートの両方を開始 ---
        StartUDPListener();
    }

    // [変更] 両方のリスナーを停止
    void OnApplicationQuit()
    {
        StopUDPListener();
    }

    // [変更] 両方のリスナーを停止
    void OnDestroy()
    {
        StopUDPListener();
    }


    // ... (HideAllMonsters, InitializeEnemies は変更なし) ...
    void HideAllMonsters()
    {
        for (int i = 0; i < currentEnemies.Length; i++) { currentEnemies[i] = null; }
        foreach (var monster in slimes) { if (monster != null) monster.gameObject.SetActive(false); }
        foreach (var monster in skeletons) { if (monster != null) monster.gameObject.SetActive(false); }
        foreach (var monster in golems) { if (monster != null) monster.gameObject.SetActive(false); }
        foreach (var monster in ghosts) { if (monster != null) monster.gameObject.SetActive(false); }
    }
    void InitializeEnemies()
    {
        HideAllMonsters();
        for (int i = 0; i < enemyActionCounters.Length; i++) { enemyActionCounters[i] = 0f; }
        List<int> validDirections = new List<int>();
        if (GameManager.instance != null) { validDirections = GameManager.instance.validCombatDirections; }
        else { validDirections = new List<int> { 0, 1, 2, 3 }; }
        List<int> availableIndices = new List<int>();
        foreach (int index in validDirections) { if (index >= 0 && index < 4) { availableIndices.Add(index); } }
        if (availableIndices.Count == 0) { availableIndices.Add(0); }
        int count = Random.Range(1, availableIndices.Count + 1);
        for (int i = 0; i < count; i++)
        {
            if (availableIndices.Count == 0) break;
            int listIndex = Random.Range(0, availableIndices.Count);
            int slotIndex = availableIndices[listIndex];
            availableIndices.RemoveAt(listIndex);
            int monsterTypeIndex = Random.Range(0, 4);
            CharacterStats monsterToActivate = null;
            switch (monsterTypeIndex)
            {
                case 0: monsterToActivate = (slimes.Length > slotIndex && slimes[slotIndex] != null) ? slimes[slotIndex] : null; break;
                case 1: monsterToActivate = (skeletons.Length > slotIndex && skeletons[slotIndex] != null) ? skeletons[slotIndex] : null; break;
                case 2: monsterToActivate = (golems.Length > slotIndex && golems[slotIndex] != null) ? golems[slotIndex] : null; break;
                case 3: monsterToActivate = (ghosts.Length > slotIndex && ghosts[slotIndex] != null) ? ghosts[slotIndex] : null; break;
            }
            if (monsterToActivate != null)
            {
                monsterToActivate.ResetHealth();
                monsterToActivate.gameObject.SetActive(true);
                currentEnemies[slotIndex] = monsterToActivate;
            }
        }
    }


    // --- [ここから修正 (Update メソッド)] ---
    void Update()
    {
        if (isBattleEnding) return;

        // --- 1. 入力フラグのリセット ---
        isW_Pressed = false;
        isA_Pressed = false;
        isS_Pressed = false;
        isD_Pressed = false;
        isSpace_Pressed = false;
        isJ_Pressed = false;
        isK_Pressed = false;
        isL_Pressed = false;

        // --- 2. UDP入力処理 (Python) (新データ形式対応) ---
        string latestDirection = "NONE"; // 向きの判定用に、最新の「向き」データを保持
        bool spaceFoundInQueue = false;
        bool jFoundInQueue = false;
        bool kFoundInQueue = false;
        bool lFoundInQueue = false;

        // 2a. キューに溜まっているデータをすべて処理
        while (udpReceivedDataQueue.TryDequeue(out string data))
        {
            string[] parts = data.Split(',');
            if (parts.Length == 0) continue; // 空データは無視

            // 2b. 向きの更新 (常に最新のデータで上書き)
            latestDirection = parts[0];

            // 2c. アクションのチェック (parts[1]以降)
            for (int i = 1; i < parts.Length; i++)
            {
                // キューの中の *どれか1つでも* アクションを含んでいたらフラグを立てる
                if (parts[i] == "SPACE") { spaceFoundInQueue = true; }
                else if (parts[i] == "J") { jFoundInQueue = true; }
                else if (parts[i] == "K") { kFoundInQueue = true; }
                else if (parts[i] == "L") { lFoundInQueue = true; }
            }
        }

        // 2d. 向きの判定 (最新のデータに基づいて行う)
        if (latestDirection == "W") isW_Pressed = true;
        else if (latestDirection == "A") isA_Pressed = true;
        else if (latestDirection == "S") isS_Pressed = true;
        else if (latestDirection == "D") isD_Pressed = true;

        // 2e. アクションの判定 (このフレームで1回でも受信していたら True にする)
        if (spaceFoundInQueue) { isSpace_Pressed = true; }
        if (jFoundInQueue) { isJ_Pressed = true; }
        if (kFoundInQueue) { isK_Pressed = true; }
        if (lFoundInQueue) { isL_Pressed = true; }


        // --- 3. [削除] シリアル入力処理 ---


        bool playerActed = false;
        bool isRoto = (GameManager.instance != null) ? GameManager.instance.isRotoActive : true;

        // [変更] シリアル("s")の判定を削除
        bool rotoAttackInput = (isRoto && isSpace_Pressed); // UDP "SPACE"

        // --- 4. 入力ロジックの実行 ---

        // 1a. 武器の「向き」変更 (キーボード OR UDP)
        if (isW_Pressed || Input.GetKeyDown(KeyCode.W)) { SetActiveWeaponDisplay(3, true); }
        else if (isS_Pressed || Input.GetKeyDown(KeyCode.S)) { SetActiveWeaponDisplay(2, true); }
        else if (isA_Pressed || Input.GetKeyDown(KeyCode.A)) { SetActiveWeaponDisplay(0, true); }
        else if (isD_Pressed || Input.GetKeyDown(KeyCode.D)) { SetActiveWeaponDisplay(1, true); }

        // 1b. 武器の「種類」切り替え (Cキー)
        if (Input.GetKeyDown(KeyCode.C))
        {
            if (GameManager.instance != null)
            {
                GameManager.instance.isRotoActive = !GameManager.instance.isRotoActive;
                SetActiveWeaponDisplay(GameManager.instance.currentWeaponDirectionIndex, false);
                Debug.Log(GameManager.instance.isRotoActive ? "武器を Roto に切り替えました。" : "武器を Rod に切り替えました。");
            }
        }

        // --- [変更なし] JKLの入力判定を統合 ---
        bool k_Input = Input.GetKeyDown(KeyCode.K) || isK_Pressed;
        bool j_Input = Input.GetKeyDown(KeyCode.J) || isJ_Pressed;
        bool l_Input = Input.GetKeyDown(KeyCode.L) || isL_Pressed;

        // 1c. 「攻撃」 (J, K, Lキー、または Roto時の入力)
        // (ロジックは変更なし)
        if (!Input.GetKeyDown(KeyCode.C) && (k_Input || j_Input || l_Input || rotoAttackInput))
        {
            if (playerStats == null) { return; }
            lastPlayedAttackSound = null;
            int activeRotoIndex = (GameManager.instance != null) ? GameManager.instance.currentWeaponDirectionIndex : 0;
            if (activeRotoIndex < 0 || activeRotoIndex >= 4) { return; }

            if (!isRoto && j_Input)
            {
                playerActed = HandleRodJKey(activeRotoIndex);
            }
            else if (!isRoto && l_Input)
            {
                playerActed = HandleRodLKey(activeRotoIndex);
            }
            else
            {
                if (activeRotoIndex < currentEnemies.Length && currentEnemies[activeRotoIndex] != null)
                {
                    CharacterStats targetEnemy = currentEnemies[activeRotoIndex];
                    if (targetEnemy.currentHealth <= 0 || !targetEnemy.gameObject.activeSelf)
                    {
                        Debug.Log($"スロット {activeRotoIndex + 1} の敵は倒されているか、そこにはいない。");
                    }
                    else
                    {
                        if (isRoto)
                        {
                            if (k_Input || rotoAttackInput)
                            {
                                playerActed = PerformAttack(targetEnemy, true, 1, activeRotoIndex);
                            }
                            else { Debug.Log("Roto 使用中は J, L キーは無効です。"); }
                        }
                        else // Rod
                        {
                            if (k_Input)
                            {
                                playerActed = PerformAttack(targetEnemy, false, 1, activeRotoIndex);
                            }
                        }
                    }
                }
                else
                {
                    if (k_Input || rotoAttackInput)
                    {
                        Debug.Log($"スロット {activeRotoIndex} には現在、敵がいません。");
                    }
                }
            }
            StartCoroutine(CheckForBattleEndCoroutine());
        }

        // 2. 敵のターン処理
        if (playerActed && !isBattleEnding)
        {
            if (playerStats != null && playerStats.currentHealth > 0)
            {
                ProcessEnemyTurns();
            }
        }
    }
    // --- [Update メソッドの修正ここまで] ---


    // ... (SetActiveWeaponDisplay ～ CheckForBattleEndCoroutine は変更なし) ...
    void SetActiveWeaponDisplay(int index, bool updateGameManager)
    {
        if (GameManager.instance == null) { return; }
        if (updateGameManager) { GameManager.instance.currentWeaponDirectionIndex = index; }
        if (index < 0 || index >= 4) { index = 0; } // 安全対策
        bool isRoto = GameManager.instance.isRotoActive;
        int directionIndex = GameManager.instance.currentWeaponDirectionIndex;
        for (int i = 0; i < 4; i++) { if (rotos[i] != null) { rotos[i].SetActive(isRoto && (i == directionIndex)); } if (rods[i] != null) { rods[i].SetActive(!isRoto && (i == directionIndex)); } }
    }
    void ProcessEnemyTurns()
    {
        if (playerStats == null || playerStats.speed <= 0) return;
        Debug.Log("--- 敵のターン開始 ---");
        for (int i = 0; i < currentEnemies.Length; i++)
        {
            CharacterStats enemy = currentEnemies[i];
            if (enemy == null || !enemy.gameObject.activeSelf || enemy.currentHealth <= 0) { continue; }
            if (enemy.speed <= 0) { continue; }
            enemyActionCounters[i] += 1.0f;
            float actionRatio = (float)playerStats.speed / (float)enemy.speed;
            while (enemyActionCounters[i] >= actionRatio)
            {
                EnemyAct(enemy);
                enemyActionCounters[i] -= actionRatio;
                if (playerStats.currentHealth <= 0 || isBattleEnding) { return; }
            }
        }
        Debug.Log("--- 敵のターン終了 ---");
    }
    void EnemyAct(CharacterStats enemy)
    {
        if (playerStats == null || playerStats.currentHealth <= 0) { return; }
        int damage = enemy.attack - playerStats.defense;
        damage = Mathf.Max(1, damage);
        Debug.Log($"<color=red>{enemy.gameObject.name} の攻撃！ Player に {damage} のダメージ！</color>");
        playerStats.TakeDamage(damage);
    }
    void OnPlayerDied()
    {
        if (isBattleEnding) return;
        isBattleEnding = true;
        Debug.Log("<color=red>プレイヤーは倒れてしまった... ゲームオーバー</color>");
        if (GameManager.instance != null) { GameManager.instance.GoToDeathScene(); }
    }
    IEnumerator CheckForBattleEndCoroutine()
    {
        yield return null;
        if (isBattleEnding) yield break;
        bool anyEnemyActive = currentEnemies.Any(e => e != null && e.currentHealth > 0 && e.gameObject.activeSelf);
        if (!anyEnemyActive)
        {
            isBattleEnding = true;
            Debug.Log("すべての敵を倒した！戦闘終了。");
            float waitTime = 0f;
            if (lastPlayedAttackSound != null) { waitTime = lastPlayedAttackSound.length; }
            if (GameManager.instance != null && playerStats != null) { GameManager.instance.playerCurrentHealth = playerStats.currentHealth; }
            if (waitTime > 0) { yield return new WaitForSeconds(waitTime); }
            if (GameManager.instance != null) { GameManager.instance.ReturnToMainScene(); }
        }
    }

    // ... (HandleRodJKey ～ HideEffectAfterDelay は変更なし) ...
    private bool HandleRodJKey(int slotIndex)
    {
        GameObject effectPrefab = null;
        if (rodAttackEffectPrefabs_J != null && slotIndex >= 0 && slotIndex < rodAttackEffectPrefabs_J.Length) { effectPrefab = rodAttackEffectPrefabs_J[slotIndex]; }
        AudioClip soundClip = rodAttackSound_J;
        isRodKAttackBoosted = true;
        Debug.Log("Rod (K) 攻撃が強化された！");
        lastPlayedAttackSound = soundClip;
        PlayEffectAndSound(effectPrefab, soundClip, slotIndex, 0);
        return true;
    }
    private bool HandleRodLKey(int slotIndex)
    {
        bool actionTakesTurn = true;
        GameObject effectPrefab = null;
        AudioClip soundClip = null;
        if (rodAttackEffectPrefabs_L != null && slotIndex >= 0 && slotIndex < rodAttackEffectPrefabs_L.Length) { effectPrefab = rodAttackEffectPrefabs_L[slotIndex]; }
        if (hasUsedRodLHeal)
        {
            Debug.Log("Rod (L) の回復は戦闘中1回しか使えない。");
            if (ineffectiveAttackSound != null) { soundClip = ineffectiveAttackSound; }
            actionTakesTurn = false;
        }
        else
        {
            soundClip = rodAttackSound_L;
            if (playerStats != null)
            {
                playerStats.Heal(rodLHealAmount);
                hasUsedRodLHeal = true;
                Debug.Log($"Rod (L) で {rodLHealAmount} HP回復した。 (現在HP: {playerStats.currentHealth})");
            }
        }
        lastPlayedAttackSound = soundClip;
        PlayEffectAndSound(effectPrefab, soundClip, slotIndex, 2);
        return actionTakesTurn;
    }
    private bool PerformAttack(CharacterStats targetEnemy, bool isRoto, int attackTypeIndex, int slotIndex)
    {
        bool actionTakesTurn = true;
        int finalDamage = 0;
        GameObject effectPrefab = null;
        AudioClip soundClip = null;
        if (isRoto)
        {
            if (rotoAttackEffectPrefabs != null && slotIndex >= 0 && slotIndex < rotoAttackEffectPrefabs.Length) { effectPrefab = rotoAttackEffectPrefabs[slotIndex]; }
            string enemyName = targetEnemy.gameObject.name.ToLower();
            bool isEffective = false;
            if (enemyName.Contains("slime") || enemyName.Contains("skeleton")) { isEffective = true; }
            if (isEffective)
            {
                int damage = playerStats.attack - targetEnemy.defense;
                finalDamage = Mathf.Max(1, damage);
                soundClip = rotoAttackSound;
                targetEnemy.TakeDamage(finalDamage);
                if (targetEnemy.currentHealth <= 0 && targetEnemy.gameObject.activeSelf)
                {
                    targetEnemy.gameObject.SetActive(false);
                    if (GameManager.instance != null) { GameManager.instance.NotifyEnemyDefeated(); }
                }
            }
            else
            {
                finalDamage = 0;
                if (ineffectiveAttackSound != null) { soundClip = ineffectiveAttackSound; }
                actionTakesTurn = false;
                Debug.Log($"Roto 攻撃は {targetEnemy.name} に無効だ。");
            }
        }
        else // Rod
        {
            if (attackTypeIndex == 1) // Kキー
            {
                if (rodAttackEffectPrefabs_K != null && slotIndex >= 0 && slotIndex < rodAttackEffectPrefabs_K.Length) { effectPrefab = rodAttackEffectPrefabs_K[slotIndex]; }
                soundClip = rodAttackSound_K;
                string enemyName = targetEnemy.gameObject.name.ToLower();
                bool isEffective = false;
                if (enemyName.Contains("golem") || enemyName.Contains("ghost")) { isEffective = true; }
                if (isEffective)
                {
                    int damage = playerStats.attack - targetEnemy.defense;
                    damage = Mathf.Max(1, damage);
                    if (isRodKAttackBoosted) { finalDamage = damage * 3; isRodKAttackBoosted = false; }
                    else { finalDamage = damage; }
                    targetEnemy.TakeDamage(finalDamage);
                    if (targetEnemy.currentHealth <= 0 && targetEnemy.gameObject.activeSelf)
                    {
                        targetEnemy.gameObject.SetActive(false);
                        if (GameManager.instance != null) { GameManager.instance.NotifyEnemyDefeated(); }
                    }
                }
                else
                {
                    finalDamage = 0;
                    if (ineffectiveAttackSound != null) { soundClip = ineffectiveAttackSound; }
                    actionTakesTurn = false;
                    Debug.Log($"Rod (K) 攻撃は {targetEnemy.name} に無効だ。");
                }
            }
        }
        lastPlayedAttackSound = soundClip;
        PlayEffectAndSound(effectPrefab, soundClip, slotIndex, attackTypeIndex);
        return actionTakesTurn;
    }
    private void PlayEffectAndSound(GameObject effectPrefab, AudioClip soundClip, int slotIndex, int attackTypeIndex)
    {
        if (effectPrefab != null)
        {
            if (activeEffectCoroutines.ContainsKey(effectPrefab) && activeEffectCoroutines[effectPrefab] != null) { StopCoroutine(activeEffectCoroutines[effectPrefab]); activeEffectCoroutines.Remove(effectPrefab); }
            effectPrefab.SetActive(true);
            Coroutine newCoroutine = StartCoroutine(HideEffectAfterDelay(effectPrefab, 1.0f));
            activeEffectCoroutines[effectPrefab] = newCoroutine;
        }
        if (audioSource != null && soundClip != null) { audioSource.PlayOneShot(soundClip); }
    }
    private System.Collections.IEnumerator HideEffectAfterDelay(GameObject effect, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (effect != null) { effect.SetActive(false); }
        if (activeEffectCoroutines.ContainsKey(effect)) { activeEffectCoroutines.Remove(effect); }
    }


    // --- [ここから変更] UDPとシリアルのメソッドを両方追加 ---

    // --- UDP受信メソッド群 ---

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

            isUdpThreadRunning = true;
            udpReceiveThread = new Thread(ReceiveUDPData);
            udpReceiveThread.IsBackground = true;
            udpReceiveThread.Start();
            Debug.Log($"UDPリスナーを開始しました。ポート: {udpListenPort}");
        }
        catch (System.Exception e) { Debug.LogError("UDPリスナーの開始に失敗しました: " + e.Message); }
    }

    private void StopUDPListener()
    {
        if (!isUdpThreadRunning) return;
        isUdpThreadRunning = false;
        if (udpClient != null)
        {
            udpClient.Close();
            udpClient = null;
        }
        if (udpReceiveThread != null && udpReceiveThread.IsAlive) { udpReceiveThread.Join(); }
        udpReceiveThread = null;
        Debug.Log("UDPリスナーを停止しました。");
    }

    private void ReceiveUDPData()
    {
        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
        while (isUdpThreadRunning)
        {
            try
            {
                if (udpClient == null) break;
                byte[] data = udpClient.Receive(ref remoteEP);
                string message = Encoding.UTF8.GetString(data);
                udpReceivedDataQueue.Enqueue(message);
            }
            catch (System.Net.Sockets.SocketException) { if (isUdpThreadRunning) { Debug.LogWarning("SocketException (UDP Clientが閉じられました)"); } }
            catch (System.Exception e) { if (isUdpThreadRunning) { Debug.LogError("UDPデータ受信エラー: " + e.Message); } }
        }
        Debug.Log("UDP受信スレッドを終了します。");
    }
}