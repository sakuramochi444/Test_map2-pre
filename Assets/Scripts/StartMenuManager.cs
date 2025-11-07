// StartMenuManager.cs (新規作成)

using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshProのInputFieldを使う場合

/// <summary>
/// StartSceneのUIを管理し、ID入力とシーン遷移を処理します。
/// </summary>
public class StartMenuManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("ユーザーIDを入力するInputField (TMP_InputField推奨)")]
    public TMP_InputField userIdField; // または public InputField userIdField;

    [Tooltip("ゲーム開始ボタン")]
    public Button startButton;

    [Tooltip("ID未入力時に表示するエラーメッセージ (任意)")]
    public GameObject errorMessageObject;

    [Header("Scene Loader")]
    [Tooltip("シーン遷移と効果音を担当する SceneLoader (StartSceneに必要)")]
    public SceneLoader sceneLoader; // インスペクタで設定

    [Tooltip("遷移先のシーン名")]
    public string mainSceneName = "MainScene"; // SceneLoader.cs と合わせる

    void Start()
    {
        // 1. ボタンにクリックイベントを登録
        if (startButton != null)
        {
            startButton.onClick.AddListener(OnStartButtonClicked);
        }

        // 2. エラーメッセージは非表示にしておく
        if (errorMessageObject != null)
        {
            errorMessageObject.SetActive(false);
        }

        // 3. 必要なコンポーネントが設定されているか確認
        if (sceneLoader == null)
        {
            Debug.LogError("StartMenuManager: SceneLoader が設定されていません！");
        }
        if (userIdField == null)
        {
            Debug.LogError("StartMenuManager: UserIdField が設定されていません！");
        }

        // 4. FlagManagerのインスタンスを確認 (StartSceneにある前提)
        if (FlagManager.instance == null)
        {
            Debug.LogWarning("FlagManager.instance がまだ存在しません。StartSceneにFlagManagerのプレハブが配置されていることを確認してください。");
        }
    }

    /// <summary>
    /// スタートボタンがクリックされたときに呼び出されます。
    /// </summary>
    public void OnStartButtonClicked()
    {
        // 必要なコンポーネントをチェック
        if (userIdField == null || sceneLoader == null || FlagManager.instance == null)
        {
            Debug.LogError("必要なコンポーネントが設定されていないか、FlagManagerが見つかりません。");
            return;
        }

        string inputId = userIdField.text;

        // 1. IDが空でないかチェック
        if (string.IsNullOrWhiteSpace(inputId))
        {
            Debug.LogWarning("ユーザーIDが入力されていません。");
            if (errorMessageObject != null)
            {
                errorMessageObject.SetActive(true); // エラーメッセージ表示
            }
            return; // 処理を中断
        }

        // 2. [変更] FlagManager にIDを一時保存
        FlagManager.instance.SetUserId(inputId);
        Debug.Log($"FlagManagerにユーザーID: {inputId} を一時保存しました。");

        // 3. エラーメッセージを隠す
        if (errorMessageObject != null)
        {
            errorMessageObject.SetActive(false);
        }

        // 4. SceneLoader を使って MainScene に遷移
        // (SceneLoaderは内部で FlagManager.NotifyDungeonPlayed() を呼び出すが、
        // この時点で FlagManager にはID (storedUserId) がセットされている)
        sceneLoader.LoadSceneWithSound(mainSceneName);
    }
}