// FlagManager.cs (修正後)

using UnityEngine;
using UnityEngine.Networking; // UnityWebRequest を使うために必要
using System.Collections;
using System.Text;

public class FlagManager : MonoBehaviour
{
    // --- [追加] シングルトンインスタンス ---
    public static FlagManager instance;

    // --- データ構造定義 ---

    // (中略: EnvData, FlagUpdateRequest, UpdateData クラス定義)
    // 1. env.json をデシリアライズするためのクラス
    // 注意: UnityのJsonUtilityは "x-api-key" のようなハイフンを認識できません。
    // そのため、JSONテキストを取得後に "x-api-key" を "x_api_key" に置換してパースします。
    [System.Serializable]
    private class EnvData
    {
        public string uri;
        public string x_api_key; // "x-api-key" を置換した "x_api_key" に対応
    }

    // 2. APIリクエスト（ボディ）のためのクラス
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
    // (中略ここまで)


    // --- 定数 ---
    // chFlag.js と getCoin.js から参照
    private const string EnvJsonUri = "https://pinattutaro.github.io/fest2025api/4u/env.json";
    private const string UserId = "gmk7F5";

    // --- [追加] シングルトンの初期化 ---
    void Awake()
    {
        // シングルトンパターンの実装
        if (instance == null)
        {
            instance = this;
            // シーンをまたいでも破棄されないようにする
            // (GameManagerなど、他のDontDestroyOnLoadオブジェクトと共存させる)
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 既にインスタンスが存在する場合は、このオブジェクトを破棄する
            Destroy(gameObject);
        }
    }


    // --- メインの関数 ---

    /// <summary>
    /// 敵の討伐総数を送信します (ButtonManagerからリセット時に呼び出されます)。
    /// </summary>
    /// <param name="amount">送信する総討伐数</param>
    public void NotifyEnemyDefeated(int amount)
    {
        // 倒した数が 0 以下なら何もしない
        if (amount <= 0)
        {
            Debug.Log($"[FlagManager] 討伐数 {amount} のため、API送信はスキップします。");
            return;
        }

        // コルーチンを開始してフラグを更新 (引数の amount を使用)
        Debug.Log($"[FlagManager] 討伐総数 {amount} でAPI送信を開始します。");
        StartCoroutine(UpdateFlagCoroutine("dungeon_enemies_defeated", amount));
    }

    /// <summary>
    /// [追加] 階層を突破した時に呼び出します。
    /// </summary>
    public void NotifyFloorCleared()
    {
        // "dungeon_floors_cleared" フラグを 1 増やす
        StartCoroutine(UpdateFlagCoroutine("dungeon_floors_cleared", 1));
    }

    /// <summary>
    /// [追加] ダンジョンプレイ回数を更新します (例: ゲーム開始時)
    /// </summary>
    public void NotifyDungeonPlayed()
    {
        // "dungeon_played" フラグを 1 増やす
        StartCoroutine(UpdateFlagCoroutine("dungeon_played", 1));
    }


    /// <summary>
    /// 実際にAPIリクエストを行うコルーチン
    /// </summary>
    /// <param name="flagName">更新するフラグ名 (README.md 参照)</param>
    /// <param name="incrementValue">増やす数</param>
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
                yield break; // エラーが発生したら処理を中断
            }

            string jsonText = envRequest.downloadHandler.text;

            // "x-api-key" を "x_api_key" に置換 (JsonUtility のため)
            string parsableJson = jsonText.Replace("\"x-api-key\":", "\"x_api_key\":");

            EnvData envData = JsonUtility.FromJson<EnvData>(parsableJson);
            baseUri = envData.uri;
            apiKey = envData.x_api_key;

            if (string.IsNullOrEmpty(baseUri) || string.IsNullOrEmpty(apiKey))
            {
                Debug.LogError("[FlagManager] env.jsonのパースに失敗。uriまたはapiKeyが空です。");
                yield break;
            }
        }

        // --- ステップ2: フラグ更新リクエストのボディを作成 ---
        FlagUpdateRequest requestBody = new FlagUpdateRequest
        {
            userId = UserId,
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
        string targetUrl = $"{baseUri}/api/users/update-flag"; // chFlag.js から

        using (UnityWebRequest apiRequest = new UnityWebRequest(targetUrl, "POST"))
        {
            apiRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            apiRequest.downloadHandler = new DownloadHandlerBuffer();

            // ヘッダーを設定 (chFlag.js 参照)
            apiRequest.SetRequestHeader("Content-Type", "application/json");
            apiRequest.SetRequestHeader("x-api-key", apiKey);

            yield return apiRequest.SendWebRequest();

            // --- ステップ4: 結果の処理 ---
            if (apiRequest.result != UnityWebRequest.Result.Success)
            {
                // chFlag.js の console.log(response.status) に相当
                Debug.LogError($"[FlagManager] フラグ更新APIエラー (Status: {apiRequest.responseCode}): {apiRequest.error}");
                Debug.LogError($"[FlagManager] エラー詳細: {apiRequest.downloadHandler.text}");
            }
            else
            {
                // chFlag.js の console.log(data.data) に相当
                Debug.Log($"[FlagManager] フラグ更新成功 (Status: {apiRequest.responseCode})");
                Debug.Log($"[FlagManager] レスポンス: {apiRequest.downloadHandler.text}");
            }
        }
    }
}