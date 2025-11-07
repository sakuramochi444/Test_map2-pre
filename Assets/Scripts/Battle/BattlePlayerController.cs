using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[RequireComponent(typeof(CharacterStats))]
[RequireComponent(typeof(AudioSource))]
public class BattlePlayerController : MonoBehaviour
{
    // ... (インスペクタで設定する変数は変更なし) ...
    [Header("ダメージエフェクト (UI)")]
    public RawImage[] damageImages = new RawImage[4];
    [Header("ダメージサウンド")]
    public AudioClip damageSound;

    private AudioSource audioSource;
    private CharacterStats playerStats;
    private Coroutine damageEffectCoroutine;

    void Start()
    {
        // ... (Startメソッドの中身は変更なし) ...
        playerStats = GetComponent<CharacterStats>();
        if (playerStats == null)
        {
            Debug.LogError("BattlePlayerController: CharacterStats が見つかりません！");
            return;
        }
        playerStats.OnDamaged.AddListener(ShowDamageEffect);
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;

        foreach (var img in damageImages)
        {
            if (img != null) img.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// OnDamagedイベントから呼び出されるメソッド。
    /// UIエフェクトとサウンド再生のコルーチンを開始します。
    /// </summary>
    private void ShowDamageEffect()
    {
        // --- [ここから変更] ---
        // 1. サウンド再生処理を削除 (コルーチン内に移動します)
        // if (audioSource != null && damageSound != null)
        // {
        //     audioSource.PlayOneShot(damageSound);
        // }
        // --- [変更ここまで] ---

        // 2. UIエフェクトのコルーチンを開始
        if (damageEffectCoroutine != null)
        {
            StopCoroutine(damageEffectCoroutine);
        }

        // --- [ここから変更] ---
        // 待機時間 (第1引数) を 1.0f に変更します。
        damageEffectCoroutine = StartCoroutine(DamageEffectCoroutine(0.5f, 0.5f));
        // --- [変更ここまで] ---
    }

    /// <summary>
    /// [delay]秒後にサウンドを再生し、同時にエフェクトを[duration]秒間表示します。
    /// </summary>
    /// <param name="delay">再生/表示するまでの待機時間（秒）</param>
    /// <param name="duration">表示する時間（秒）</param>
    private IEnumerator DamageEffectCoroutine(float delay, float duration)
    {
        // 1. 指定された時間 (delay = 1秒) だけ待機
        yield return new WaitForSeconds(delay);

        // --- [ここから追加] ---
        // 2. 1秒後、サウンドを再生
        if (audioSource != null && damageSound != null)
        {
            audioSource.PlayOneShot(damageSound);
        }
        // --- [ここまで追加] ---

        // 3. (サウンドと同時に) 画像を表示
        foreach (var img in damageImages)
        {
            if (img != null)
            {
                img.gameObject.SetActive(true);
            }
        }

        // 4. 指定された時間 (duration = 0.5秒) だけ待機
        yield return new WaitForSeconds(duration);

        // 5. 画像を非表示
        foreach (var img in damageImages)
        {
            if (img != null)
            {
                img.gameObject.SetActive(false);
            }
        }

        // 6. 管理用の変数を空に戻す
        damageEffectCoroutine = null;
    }
}