// CharacterStats.cs
using UnityEngine;
using UnityEngine.Events; // UnityEvent (インスペクタから関数を呼ぶため) に必要

// プレイヤーや敵の共通ステータス（HP、攻撃力、防御力、速度）を管理するコンポーネントです。
// ダメージ処理や死亡時のイベント発行も担当します。
public class CharacterStats : MonoBehaviour
{
    [Header("基本ステータス")]
    [Tooltip("最大体力")]
    public int maxHealth = 100;

    // [SerializeField] をつけると、private変数でもインスペクタには表示されます。
    // (デバッグや初期値設定には便利ですが、他のスクリプトからは直接変更できません)
    [Tooltip("現在の体力")]
    [SerializeField]
    private int _currentHealth;

    // _currentHealth を外部から安全に読み書きするための「プロパティ」です。
    // (例: Player.currentHealth = 50; のように代入できます)
    public int currentHealth
    {
        // currentHealth の値を読み取ろうとした時 (get)
        get { return _currentHealth; }

        // currentHealth に値を代入しようとした時 (set)
        set
        {
            _currentHealth = value; // (value は代入しようとした値)

            // HPが0未満になったり、maxHealth を超えたりしないように、
            // Mathf.Clamp で値を (0 ～ maxHealth) の範囲に丸めます。
            _currentHealth = Mathf.Clamp(_currentHealth, 0, maxHealth);
        }
    }

    [Tooltip("攻撃力")]
    public int attack = 10;

    [Tooltip("防御力")]
    public int defense = 5;

    [Tooltip("速度（行動順などに使用）")]
    public int speed = 10;

    [Header("イベント")]
    // UnityEvent を使うと、インスペクタ上で
    // 「このイベントが起きたら、他のオブジェクトのこの関数を実行する」
    // という設定をドラッグ＆ドロップで行えます。

    [Tooltip("TakeDamage() が呼ばれた時に発生するイベント")]
    public UnityEvent OnDamaged;

    [Tooltip("HPが0になり Die() が呼ばれた時に発生するイベント")]
    public UnityEvent OnDied;

    // オブジェクトが有効化された時（またはゲーム開始時）に呼ばれます。
    void Awake()
    {
        // 体力を最大値で初期化します。
        _currentHealth = maxHealth;
    }

    // ダメージを受ける処理
    // damageAmount: （攻撃力 - 防御力）などの計算が「済んだ後」の最終ダメージ値
    public void TakeDamage(int damageAmount)
    {
        // すでにHPが0以下の場合は、何も処理しない
        if (_currentHealth <= 0) return;

        // ダメージを適用
        _currentHealth -= damageAmount;

        // HPがマイナスにならないようにする (Max(0, -10) -> 0 になる)
        _currentHealth = Mathf.Max(_currentHealth, 0);

        Debug.Log(gameObject.name + " は " + damageAmount + " のダメージを受けた。残りHP: " + _currentHealth);

        // OnDamaged イベントに登録されている処理（UIのHPバー更新など）を実行
        // ( ?.Invoke() は、登録されている処理が何もない(null)場合はエラーにならず、何もしない)
        OnDamaged?.Invoke();

        // 死亡判定
        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    // 死亡処理（HPが0になった時に TakeDamage から呼ばれます）
    private void Die()
    {
        Debug.Log(gameObject.name + " は倒れた。");

        // OnDied イベントに登録されている処理
        // (例: BattleGameManagerのOnPlayerDied や、敵が倒れた時の処理) を実行
        OnDied?.Invoke();

        // (このスクリプト自体は、オブジェクトを非表示(SetActive(false))にしたりはしません。
        //  死亡時の処理は、OnDiedイベントを受け取った BattleGameManager などが担当します。)
    }

    // (参考) 回復処理
    public void Heal(int amount)
    {
        _currentHealth += amount;

        // 最大HPを超えないようにする (Min(120, 100) -> 100 になる)
        _currentHealth = Mathf.Min(_currentHealth, maxHealth);
    }

    // HPを全快にします（例：敵の再配置時など）
    public void ResetHealth()
    {
        _currentHealth = maxHealth;
    }

    // GameManagerなどからステータスを一括で設定（初期化）する
    // (例: 戦闘シーン開始時にプレイヤーのステータスを復元する時)
    public void InitializeStats(int maxHP, int currentHP, int atk, int def, int spd)
    {
        maxHealth = maxHP;
        attack = atk;
        defense = def;
        speed = spd;

        // currentHealth への代入を最後に実行します。
        // これにより、プロパティ(set)内の Clamp処理 が、
        // 更新された「maxHealth」を正しく使って実行されることが保証されます。
        currentHealth = currentHP;
    }
}