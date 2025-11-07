using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class DynamicButtonListener : MonoBehaviour
{
    // ★ 1. Inspectorでわかりやすいよう、メソッドの種類を定義
    // (例: StartGame, OpenSettings, QuitGame など、実際の機能名に変えてください)
    public enum ButtonActionType
    {
        ActionA,
        ActionB,
        ActionC,
        ActionD,
    }

    // Inspector上で、このボタンがどのアクションを実行するかを選択します
    [SerializeField]
    private ButtonActionType actionType;

    private Button button;

    void Start()
    {
        button = GetComponent<Button>();

        // (修正点 1) GameManagerではなく、ButtonManager のコンポーネントを探す
        ButtonManager buttonManager = FindFirstObjectByType<ButtonManager>();

        if (buttonManager != null)
        {
            // 古いリスナーをクリア
            button.onClick.RemoveAllListeners();

            // Inspectorで選択された actionType に応じて、登録するメソッドを切り替える
            switch (actionType)
            {
                // ★ 2. ActionA が選ばれた場合
                case ButtonActionType.ActionA:
                    // (修正点 2) buttonManager の中のメソッドを呼ぶ
                    // ↓↓↓↓ ButtonManager の「1つ目のメソッド名」に書き換えてください ↓↓↓↓
                    button.onClick.AddListener(buttonManager.ResetAndReturnToStart);
                    break;

                // ★ 3. ActionB が選ばれた場合
                case ButtonActionType.ActionB:
                    // ↓↓↓↓ ButtonManager の「2つ目のメソッド名」に書き換えてください ↓↓↓↓
                    button.onClick.AddListener(buttonManager.QuitGame);
                    break;

                // ★ 4. ActionC が選ばれた場合
                case ButtonActionType.ActionC:
                    // ↓↓↓↓ ButtonManager の「3つ目のメソッド名」に書き換えてください ↓↓↓↓
                    button.onClick.AddListener(buttonManager.HealPlayerToFull);
                    break;

                case ButtonActionType.ActionD:
                    // ↓↓↓↓ ButtonManager の「4つ目のメソッド名」に書き換えてください ↓↓↓↓
                    button.onClick.AddListener(buttonManager.ClearChange);
                    break;

                default:
                    Debug.LogWarning($"ボタン '{gameObject.name}' のアクションタイプが設定されていません。");
                    break;
            }
        }
        else
        {
            Debug.LogError("ButtonManager が見つかりませんでした。ボタンイベントを登録できません。");
        }
    }
}