// FlagManager.cs (修正・完全版)

using UnityEngine;
using UnityEngine.Networking; // UnityWebRequest を使うために必要
using System.Collections;
using System.Text;

public class FlagManager : MonoBehaviour
{
    public static FlagManager instance;

    // StartSceneで設定され、GameManagerに引き継がれるまで一時的にIDを保持する
    private string storedUserId = "";

    // --- [ここから追加] APIリクエスト用のデータ構造定義 ---

    [System.Serializable]
    private class EnvData
    {
        public string uri;
        public string x_api_key; // JSONの "x-api-key" と一致させる
    }

    [System.Serializable]
    private class FlagUpdateRequest
    {
        public string userId;
        public UpdateData[] updates;
    }

    [System.Serializable]
    private class UpdateData
    {
        public string flagName;
        public int increment;
    }

    // --- [追加ここまで] ---


    private const string EnvJsonUri = "https://pinattutaro.github.io/fest2025api/4u/env.json";

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // --- [ここから追加] ID管理メソッド (変更なし) ---
    /// <summary>
    /// StartSceneから呼び出され、ユーザーIDを一時的に保存します。
    /// </summary>
    public void SetUserId(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[FlagManager] 空のIDが設定されようとしました。");
            return;
        }
        storedUserId = id;
        Debug.Log($"[FlagManager] ユーザーID '{storedUserId}' を一時保存しました。");
    }

    /// <summary>
    /// GameManagerにIDを引き渡すために使用します。
    /// </summary>
    public string GetStoredUserId()
    {
        return storedUserId;
    }

    /// <summary>
    /// IDが設定されているか確認します（主にGameManagerが使用）
    /// </summary>
    public bool HasStoredUserId()
    {
        return !string.IsNullOrEmpty(storedUserId);
    }
    // --- [追加ここまで] ---


    // --- [ここから追加] 他のスクリプトから呼び出されるメソッド群 ---

    /// <summary>
    /// [ButtonManager用] 敵の総討伐数をAPIに送信します。
    /// </summary>
    public void NotifyEnemyDefeated(int killCount)
    {
        if (killCount <= 0)
        {
            Debug.Log("[FlagManager] 討伐数が0のため、API送信をスキップします。");
            return;
        }

        // "enemies_defeated" フラグを killCount の値だけ増やす
        StartCoroutine(UpdateFlagCoroutine("dungeon_enemies_defeated", killCount));
        Debug.Log($"[FlagManager] API送信開始: dungeon_enemies_defeated (+{killCount})");
    }

    /// <summary>
    /// [PlayerController用] 階層クリアをAPIに送信します。
    /// </summary>
    public void NotifyFloorCleared()
    {
        // "dungeon_floors_cleared" フラグを 1 増やす
        StartCoroutine(UpdateFlagCoroutine("dungeon_floors_cleared", 1));
        Debug.Log("[FlagManager] API送信開始: dungeon_floors_cleared (+1)");
    }

    /// <summary>
    /// [SceneLoader / ButtonManager用] ダンジョンプレイ回数をAPIに送信します。
    /// </summary>
    public void NotifyDungeonPlayed()
    {
        // "dungeon_played" フラグを 1 増やす
        StartCoroutine(UpdateFlagCoroutine("dungeon_played", 1));
        Debug.Log("[FlagManager] API送信開始: dungeon_played (+1)");
    }

    // --- [追加ここまで] ---


    /// <summary>
    /// 実際にAPIリクエストを行うコルーチン
    /// </summary>
    private IEnumerator UpdateFlagCoroutine(string flagName, int incrementValue)
    {
        // --- ステップ1: env.json から URI と APIキーを取得 ---
        string baseUri = null;
        string apiKey = null;

        using (UnityWebRequest envRequest = UnityWebRequest.Get(EnvJsonUri))
        {
            yield return envRequest.SendWebRequest();

            if (envRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[FlagManager] env.jsonの取得に失敗: {envRequest.error}");
                yield break;
            }

            string jsonText = envRequest.downloadHandler.text;
            // "x-api-key" を "x_api_key" に置換 (C#の変数名と合わせるため)
            string parsableJson = jsonText.Replace("\"x-api-key\":", "\"x_api_key\":");

            EnvData envData = JsonUtility.FromJson<EnvData>(parsableJson);
            baseUri = envData.uri;
            apiKey = envData.x_api_key;

            if (string.IsNullOrEmpty(baseUri) || string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[FlagManager] env.jsonからURIまたはAPIキーを取得できませんでした。");
                yield break;
            }
        }


        // --- ステップ2: フラグ更新リクエストのボディを作成 ---
        string currentUserId = "";

        // 1. まず GameManager (MainScene以降) からIDを取得しようと試みる
        if (GameManager.instance != null && !string.IsNullOrEmpty(GameManager.instance.userId))
        {
            currentUserId = GameManager.instance.userId;
        }
        // 2. GameManagerにない場合 (例: StartScene -> MainScene 遷移直後のNotifyDungeonPlayed)
        //    FlagManagerが一時保存しているID (storedUserId) を使用する
        else if (!string.IsNullOrEmpty(storedUserId))
        {
            currentUserId = storedUserId;
        }
        else
        {
            // どちらにもIDがない場合 (StartSceneで入力されていない場合)
            Debug.LogError("[FlagManager] ユーザーIDが GameManager にも FlagManager にも設定されていません！ API送信を中止します。");
            yield break;
        }

        FlagUpdateRequest requestBody = new FlagUpdateRequest
        {
            userId = currentUserId, // 取得したIDを使用
            updates = new UpdateData[]
            {
                new UpdateData
                {
                    flagName = flagName,
                    increment = incrementValue
                }
            }
        };

        string jsonBody = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        // --- ステップ3: APIリクエストを作成・送信 (POST) ---
        string targetUrl = $"{baseUri}/api/users/update-flag";

        using (UnityWebRequest apiRequest = new UnityWebRequest(targetUrl, "POST"))
        {
            apiRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            apiRequest.downloadHandler = new DownloadHandlerBuffer();
            apiRequest.SetRequestHeader("Content-Type", "application/json");
            apiRequest.SetRequestHeader("x-api-key", apiKey);

            yield return apiRequest.SendWebRequest();

            if (apiRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"[FlagManager] フラグ更新APIエラー (Status: {apiRequest.responseCode}): {apiRequest.error}");
                Debug.LogError($"[FlagManager] エラー詳細: {apiRequest.downloadHandler.text}");
            }
            else
            {
                Debug.Log($"[FlagManager] フラグ更新成功 (Status: {apiRequest.responseCode})");
                Debug.Log($"[FlagManager] レスポンス: {apiRequest.downloadHandler.text}");
            }
        }
    }
}