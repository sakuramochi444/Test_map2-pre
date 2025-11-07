// ButtonManager.cs (修正後)

using UnityEngine;
using UnityEngine.SceneManagement; // シーン管理に必要

/// <summary>
/// UIボタンのイベント（OnClick）から呼び出される各種機能を提供します。
/// ポーズメニューやデバッグメニューのCanvasなどにアタッチして使用します。
/// </summary>
public class ButtonManager : MonoBehaviour
{
    [Header("シーン名設定")]
    [Tooltip("リセット時に戻るスタートシーンの名前")]
    public string startSceneName = "StartScene";

    // === ゲームのリセット ===

    /// <summary>
    /// すべての状態をリセットし、指定された "StartScene" に戻ります。
    /// ボタンの OnClick() イベントに設定してください。
    /// </summary>
    public void ResetAndReturnToStart()
    {
        Debug.Log($"ゲームの状態をリセットし、{startSceneName} に戻ります。");

        // --- [ここから変更] ---

        // 0. FlagManager と GameManager が存在するか確認
        if (FlagManager.instance != null && GameManager.instance != null)
        {
            // 0a. GameManagerから総討伐数を取得
            int totalKills = GameManager.instance.totalKillCount;
            Debug.Log($"このセッションの総討伐数: {totalKills} を FlagManager に送信します。");

            // 0b. FlagManager のメソッドを呼び出し、総討伐数を送信
            // (FlagManager側で 0 以下の場合はスキップされます)
            FlagManager.instance.NotifyEnemyDefeated(totalKills);
        }
        else
        {
            Debug.LogWarning("FlagManager または GameManager が見つからないため、討伐数の送信をスキップします。");
        }

        // --- [変更ここまで] ---


        // 1. GameManager (DontDestroyOnLoad) を破棄する
        if (GameManager.instance != null)
        {
            Destroy(GameManager.instance.gameObject);
            GameManager.instance = null; // [修正] static変数を明示的にnullにする
            Debug.Log("GameManagerインスタンスを破棄しました。");
        }

        // 2. FlagManager (DontDestroyOnLoad) も破棄する
        //    (StartSceneに配置されているシングルトンをすべてリセットする)
        if (FlagManager.instance != null)
        {
            Destroy(FlagManager.instance.gameObject);
            FlagManager.instance = null; // [修正] static変数を明示的にnullにする
            Debug.Log("FlagManagerインスタンスを破棄しました。");
        }

        // 3. StartSceneをロードする
        SceneManager.LoadScene(startSceneName);
    }

    // === ゲームの終了 ===

    /// <summary>
    /// ゲームを終了します。（ビルド版でのみ有効）
    /// ボタンの OnClick() イベントに設定してください。
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("ゲームを終了します...");

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // === HPの全回復 ===

    /// <summary>
    /// プレイヤーのHPを（GameManagerと現在のシーンの両方で）全回復します。
    /// ボタンの OnClick() イベントに設定してください。
    /// </summary>
    public void HealPlayerToFull()
    {
        Debug.Log("プレイヤーのHPを全回復します。");

        // 1. GameManagerのデータを回復
        if (GameManager.instance != null && GameManager.instance.IsPlayerStatsInitialized())
        {
            GameManager.instance.playerCurrentHealth = GameManager.instance.playerMaxHealth;
            Debug.Log($"GameManagerのHPを回復: {GameManager.instance.playerCurrentHealth}/{GameManager.instance.playerMaxHealth}");
        }
        else
        {
            Debug.LogWarning("GameManagerが見つからないか、ステータスが未初期化のため、GameManager上のHPは回復できませんでした。");
        }

        // 2. 現在のシーンのプレイヤーのHPを即時回復
        BattleGameManager b_gm = FindFirstObjectByType<BattleGameManager>();
        if (b_gm != null && b_gm.playerStats != null)
        {
            b_gm.playerStats.Heal(b_gm.playerStats.maxHealth);
            Debug.Log($"BattleSceneのプレイヤーHPを回復: {b_gm.playerStats.currentHealth}/{b_gm.playerStats.maxHealth}");
            b_gm.playerStats.OnDamaged?.Invoke();
            return;
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            CharacterStats mainPlayerStats = playerObj.GetComponent<CharacterStats>();
            if (mainPlayerStats != null)
            {
                mainPlayerStats.Heal(mainPlayerStats.maxHealth);
                Debug.Log($"MainSceneのプレイヤーHPを回復: {mainPlayerStats.currentHealth}/{mainPlayerStats.maxHealth}");
                mainPlayerStats.OnDamaged?.Invoke();
            }
        }
    }

    /// <summary>
    /// 最後の階層（GameManagerが記憶している戦闘突入前の状態）からやり直します。
    /// DeathScene のボタン OnClick() イベントに設定してください。
    /// </summary>
    public void RestartFromLastFloor()
    {
        if (GameManager.instance != null)
        {
            Debug.Log("現在の階層を最初からやり直します。");

            // --- [ここから追加] ---
            // FlagManager が存在すれば、dungeon_played を送信する
            // (FlagManager は StartScene で生成され、DontDestroyOnLoad されているはず)
            if (FlagManager.instance != null)
            {
                // FlagManager.instance.NotifyDungeonPlayed();
                Debug.Log("API: dungeon_played (リスタート)");
            }
            else
            {
                // StartSceneにFlagManagerが配置されていないか、
                // 何らかの理由で破棄された場合の警告
                Debug.LogWarning("リスタート時に FlagManager.instance が見つかりませんでした。");
            }
            // --- [追加ここまで] ---

            // GameManager のリスタート処理を呼び出す
            GameManager.instance.RestartCurrentLevel();
        }
        else
        {
            // GameManager が何らかの理由で存在しない場合のフォールバック
            Debug.LogWarning("GameManagerが見つからないため、StartSceneに戻ります。");
            ResetAndReturnToStart();
        }
    }
}