using UnityEngine;

// ゲーム起動時に、PCに接続されている複数のディスプレイを有効化（アクティベート）します。
public class ActivateAllDisplays : MonoBehaviour
{
    // 有効化するディスプレイの最大数。
    // (例: 5に設定すると、Display 1からDisplay 5までが有効化の対象となります)
    private const int MaxDisplaysToActivate = 5;

    // Unityのライフサイクルで、シーン開始時に一度だけ呼び出されます。
    void Start()
    {
        Debug.Log($"接続されているディスプレイ数: {Display.displays.Length}");

        // Display 1 (インデックス 0) は常にデフォルトで有効化されています。
        // そのため、Display 2 (インデックス 1) から処理を開始します。

        // 接続されているディスプレイの数、かつ設定した最大数（MaxDisplaysToActivate）に達するまでループします。
        // (i < MaxDisplaysToActivate は、i が 1, 2, 3, 4 まで実行されることを意味します)
        for (int i = 1; i < Display.displays.Length && i < MaxDisplaysToActivate; i++)
        {
            Debug.Log($"Display {i + 1} (インデックス {i}) を有効化します。");

            // 対象のディスプレイを有効化します。
            Display.displays[i].Activate();
        }
    }
}