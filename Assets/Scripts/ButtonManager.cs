// ButtonManager.cs (修正後)

using System.Collections; // [追加] コルーチン (WaitForSeconds) のために必要
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
    [Tooltip("クリアシーンの名前")]
    public string finishSceneName = "VictoryScene";

    // === ゲームのリセット ===

    /// <summary>
    /// すべての状態をリセットし、指定された "StartScene" に戻ります。
    /// ボタンの OnClick() イベントに設定してください。
    /// </summary>
    public void ResetAndReturnToStart()
    {
        // [変更] 実際のリセット処理を行うコルーチンを開始する
        StartCoroutine(ResetAndReturnCoroutine());
    }

    // [新規追加] 討伐数送信を待機してからシーン遷移するコルーチン
    private IEnumerator ResetAndReturnCoroutine()
    {
        Debug.Log($"ゲームの状態をリセットし、{startSceneName} に戻ります。");

        bool needsApiCall = false;
        int totalKills = 0;

        // 0. FlagManager と GameManager が存在するか確認
        if (FlagManager.instance != null && GameManager.instance != null)
        {
            // 0a. GameManagerから総討伐数を取得
            totalKills = GameManager.instance.totalKillCount;

            // 0b. 討伐数が 1 以上の場合のみ送信処理を行う
            if (totalKills > 0)
            {
                needsApiCall = true;
                Debug.Log($"このセッションの総討伐数: {totalKills} を FlagManager に送信します。");

                // FlagManager のメソッドを呼び出し、総討伐数を送信
                FlagManager.instance.NotifyEnemyDefeated(totalKills);
            }
            else
            {
                Debug.Log("討伐数が0のため、API送信をスキップします。");
            }
        }
        else
        {
            Debug.LogWarning("FlagManager または GameManager が見つからないため、討伐数の送信をスキップします。");
        }

        // [重要] API送信処理（非同期）が完了するのを待機する
        if (needsApiCall)
        {
            Debug.Log("API送信が完了するのを待機します... (2.0秒)");
            // WebRequestが完了するのに十分と思われる時間を待つ
            // (FlagManagerがDestroyされる前に処理を完了させるため)
            yield return new WaitForSeconds(2.0f);
        }
        else
        {
            // API送信が不要な場合も、Destroyが同一フレームで行われるのを避けるため1フレーム待機
            yield return null;
        }

        // 1. GameManager (DontDestroyOnLoad) を破棄する
        if (GameManager.instance != null)
        {
            Destroy(GameManager.instance.gameObject);
            GameManager.instance = null;
            Debug.Log("GameManagerインスタンスを破棄しました。");
        }

        // 2. FlagManager (DontDestroyOnLoad) も破棄する
        if (FlagManager.instance != null)
        {
            Destroy(FlagManager.instance.gameObject);
            FlagManager.instance = null;
            Debug.Log("FlagManagerインスタンスを破棄しました。");
        }

        // 3. StartSceneをロードする
        SceneManager.LoadScene(startSceneName);
    }


    // === ゲームの終了 ===
    // (QuitGame メソッドは変更なし)
    public void QuitGame()
    {
        Debug.Log("ゲームを終了します...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // === HPの全回復 ===
    // (HealPlayerToFull メソッドは変更なし)
    public void HealPlayerToFull()
    {
        Debug.Log("プレイヤーのHPを全回復します。");
        if (GameManager.instance != null && GameManager.instance.IsPlayerStatsInitialized())
        {
            GameManager.instance.playerCurrentHealth = GameManager.instance.playerMaxHealth;
            Debug.Log($"GameManagerのHPを回復: {GameManager.instance.playerCurrentHealth}/{GameManager.instance.playerMaxHealth}");
        }
        else
        {
            Debug.LogWarning("GameManagerが見つからないか、ステータスが未初期化のため、GameManager上のHPは回復できませんでした。");
        }
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

    // === 最後の階層からリスタート ===
    // (RestartFromLastFloor メソッドは変更なし)
    public void RestartFromLastFloor()
    {
        if (GameManager.instance != null)
        {
            Debug.Log("現在の階層を最初からやり直します。");
            if (FlagManager.instance != null)
            {
                // FlagManager.instance.NotifyDungeonPlayed(); //
                Debug.Log("API: dungeon_played (リスタート)");
            }
            else
            {
                Debug.LogWarning("リスタート時に FlagManager.instance が見つかりませんでした。");
            }
            GameManager.instance.RestartCurrentLevel();
        }
        else
        {
            Debug.LogWarning("GameManagerが見つからないため、StartSceneに戻ります。");
            // [変更] ResetAndReturnCoroutine を呼び出すように変更
            StartCoroutine(ResetAndReturnCoroutine());
        }
    }

    public void ClearChange()
    {
        Debug.Log($"クリア画面に遷移します。");

        // 3. StartSceneをロードする
        SceneManager.LoadScene(finishSceneName);
    }
}