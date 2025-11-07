// SceneLoader.cs (修正後)

using System.Collections; // コルーチンのために必要
using UnityEngine;
using UnityEngine.SceneManagement;

// このスクリプトがアタッチされたGameObjectに
// AudioSourceコンポーネントを必須にする
[RequireComponent(typeof(AudioSource))]
public class SceneLoader : MonoBehaviour
{
    private AudioSource audioSource;

    // --- [ここから追加] ---
    [Header("シーン名設定")]
    [Tooltip("フラグを立てる対象のメインシーン名")]
    public string mainSceneName = "MainScene"; // GameManager.cs などと名前を合わせてください
    // --- [追加ここまで] ---


    // ゲーム開始時に1回だけ呼ばれる
    void Awake()
    {
        // このGameObjectに付いているAudioSourceコンポーネントを取得
        audioSource = GetComponent<AudioSource>();
    }

    // ★ボタンのOn Click()から呼び出すためのメソッド
    public void LoadSceneWithSound(string sceneName)
    {
        // 直接シーンをロードする代わりに、コルーチンを開始する
        StartCoroutine(LoadSceneRoutine(sceneName));
    }

    // 音を鳴らして待機し、シーンをロードする一連の流れ
    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        // 1. AudioSourceに設定された音を再生
        audioSource.Play();

        // --- [ここから修正] ---
        // 2. もしロード先のシーン名が "MainScene" (または指定した名前) だったら
        if (sceneName == mainSceneName)
        {
            // FlagManager のインスタンスが存在するか確認
            if (FlagManager.instance != null)
            {
                // dungeon_played フラグを送信
                FlagManager.instance.NotifyDungeonPlayed();
                Debug.Log("API: dungeon_played (SceneLoaderより送信)");
            }
            else
            {
                // このエラーが出る場合、StartSceneに FlagManager が配置されていません
                Debug.LogError("FlagManager.instance が見つかりません！ StartScene に FlagManager を配置してください。");
            }
        }
        // --- [修正ここまで] ---


        // 3. 音が鳴り終わるまで待機
        // （クリック音が一瞬の場合、0.3秒などの固定値でもOK）
        // yield return new WaitForSeconds(0.3f); 

        // または、クリップの長さだけ正確に待つ場合
        yield return new WaitForSeconds(audioSource.clip.length);

        // 4. 待機後、シーンをロード
        SceneManager.LoadScene(sceneName);
    }
}