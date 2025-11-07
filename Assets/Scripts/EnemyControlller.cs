using UnityEngine;
using System.Collections.Generic;
using System.Linq; // MoveToRandomValidSpot で .Any() を使うために必要

// 敵キャラクターのターン制の行動（追跡、ランダム移動など）を制御します。
public class EnemyController : MonoBehaviour
{
    // 参照
    private Transform playerTransform; // プレイヤーのTransform
    private CharacterController cc; // 敵自身のCharacterController

    void Start()
    {
        // "Player" タグを持つオブジェクトを検索し、そのTransformをキャッシュします。
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }

        // 自身のCharacterControllerコンポーネントを取得します。
        cc = GetComponent<CharacterController>();
    }

    /// <summary>
    // 敵のターンが来たときに呼び出され、行動を実行します。
    // 引数: playerPosBeforeMove – プレイヤーがこのターンに移動する「前」の位置
    public void ExecuteTurn(Vector3 playerPosBeforeMove)
    {
        // GameManagerによってこのフレームで戦闘が開始された場合、敵は行動しません。
        // (例: プレイヤーが先に攻撃した場合など)
        if (GameManager.instance != null && GameManager.instance.combatInitiatedThisFrame)
        {
            return;
        }

        // 行動（追跡、ランダム移動、または留まる）を決定します。
        // プレイヤーの移動前の位置を渡して、追跡判定に使用します。
        DecideActionWithProbability(playerPosBeforeMove);
    }

    // 確率に基づいて行動（追跡 > 留まる > ランダム移動）を決定します。
    void DecideActionWithProbability(Vector3 playerPosForChaseCheck)
    {
        // 1. プレイヤーの追跡を試みます。
        // 追跡（プレイヤーがいた方向に1マス進む）に成功した場合、このターンの行動は終了です。
        if (TryChasePlayer(playerPosForChaseCheck))
        {
            return;
        }

        // 2. 追跡しない場合、確率で行動を決定します。
        float randomValue = Random.value; // 0.0f から 1.0f の間のランダムな値

        // 10%の確率で「留まる」（何もしない）
        if (randomValue < 0.1f)
        {
            // 行動終了
            return;
        }
        // 3. 残りの確率で「ランダム移動」を実行します。
        else
        {
            MoveToRandomValidSpot();
        }
    }


    // 敵が向いている方向にプレイヤーが（移動前に）いたかを確認し、
    // いた場合はその方向へ1マス移動を試みます。
    // 戻り値: 追跡移動に成功した場合はtrue
    bool TryChasePlayer(Vector3 playerPosForChaseCheck)
    {
        // 1. 敵の「前方」がどちらの軸を向いているかを、Y軸の回転角度から正確に判定します。
        float yAngle = transform.rotation.eulerAngles.y;
        Vector3 forwardDir = Vector3.zero;

        // 角度（オイラー角）に基づいて、(0, 0, 1) や (1, 0, 0) といった正確な方向ベクトルを求めます。
        if (Mathf.Abs(yAngle - 0) < 1.0f || Mathf.Abs(yAngle - 360) < 1.0f) { forwardDir = new Vector3(0, 0, 1); } // Z+ (北)
        else if (Mathf.Abs(yAngle - 90) < 1.0f) { forwardDir = new Vector3(1, 0, 0); } // X+ (東)
        else if (Mathf.Abs(yAngle - 180) < 1.0f) { forwardDir = new Vector3(0, 0, -1); } // Z- (南)
        else if (Mathf.Abs(yAngle - 270) < 1.0f || Mathf.Abs(yAngle - (-90)) < 1.0f) { forwardDir = new Vector3(-1, 0, 0); } // X- (西)
        else
        {
            // 角度がぴったりでない場合の保険として、最も近い軸方向を取得します。
            forwardDir = GetRoundedDirection(transform.forward);
        }

        // 2. プレイヤーの相対位置を、引数で受け取った「移動前の位置」から算出します。
        Vector3 playerRelativePos = playerPosForChaseCheck - transform.position;

        // 3. プレイヤーが敵の「前方」マスに（移動前に）いたかチェックします。
        if (IsPlayerInDirection(playerRelativePos, forwardDir))
        {
            // 4. 移動先（敵の前方1マス）が移動可能かチェックします。
            Vector3 targetPosition = transform.position + forwardDir;

            // マップ座標に変換して範囲チェック (マップサイズは16x16、オフセット7.5fと仮定)
            int mapX = Mathf.RoundToInt(targetPosition.x + 7.5f);
            int mapZ = Mathf.RoundToInt(targetPosition.z + 7.5f);
            if (mapX < 0 || mapX >= 16 || mapZ < 0 || mapZ >= 16) { return false; } // マップ範囲外

            // マップチップの種類をチェック (1:壁, 2:障害物 と仮定)
            int targetCellType = MapGenerator.map[mapZ, mapX];
            if (targetCellType == 1 || targetCellType == 2) { return false; } // 移動不可マス

            // 他の敵がいないかチェック
            if (IsEnemyAt(targetPosition)) { return false; }

            // 5. 移動実行
            // CharacterControllerは物理挙動（衝突）を制御するため、
            // transform.positionで直接座標を上書きする場合は一時的に無効化する必要があります。
            if (cc != null) cc.enabled = false;
            transform.position = targetPosition;
            if (cc != null) cc.enabled = true;

            Debug.Log("プレイヤーを追従します。");
            return true; // 追跡成功
        }

        return false; // プレイヤーが前方にいなかった
    }

    // プレイヤーの相対位置(playerRelativePos)が、指定した方向(direction)と
    // (Y軸を無視して)一致するかどうかを判定します。
    bool IsPlayerInDirection(Vector3 playerRelativePos, Vector3 direction)
    {
        // 高低差を無視するため、XZ平面上のベクトルで比較します。
        Vector3 relativePosXZ = new Vector3(playerRelativePos.x, 0, playerRelativePos.z);

        // 2つのベクトルがほぼ同じ（距離が0.1f未満）であれば、同じ方向とみなします。
        if (Vector3.Distance(relativePosXZ, direction) < 0.1f)
        {
            return true;
        }
        return false;
    }

    // transform.forwardのような連続的なベクトルを、(1,0,0)や(0,0,-1)のような
    // 4方向の軸に沿ったベクトルに丸めます（正規化します）。
    Vector3 GetRoundedDirection(Vector3 direction)
    {
        // X成分の絶対値がZ成分の絶対値より大きい場合 (東西方向)
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.z))
        {
            return new Vector3(Mathf.Sign(direction.x), 0, 0);
        }
        // Z成分の絶対値がX成分の絶対値より大きい場合 (南北方向)
        else
        {
            return new Vector3(0, 0, Mathf.Sign(direction.z));
        }
    }


    // 移動可能な隣接マス（前後左右）からランダムに1マスを選んで移動し、その方向を向きます。
    void MoveToRandomValidSpot()
    {
        // 1. 移動可能な行き先リストを取得します。
        List<Vector3> possibleMoveDestinations = GetPossibleMoveDestinations();

        // 2. 移動先が1つでもあるか確認します。 (.Any() は Linq の機能です)
        if (possibleMoveDestinations.Any())
        {
            // 3. 移動先リストからランダムに1つを選びます。
            int randomIndex = Random.Range(0, possibleMoveDestinations.Count);
            Vector3 chosenDestination = possibleMoveDestinations[randomIndex];
            Vector3 moveDirection = chosenDestination - transform.position;

            // 4. 移動する方向を向きます。
            if (moveDirection != Vector3.zero) // 念のため、移動方向がゼロでないことを確認
            {
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
                transform.rotation = targetRotation;
            }

            // 5. 座標を直接指定して移動します (CharacterControllerを一時的に無効化)。
            if (cc != null) cc.enabled = false;
            transform.position = chosenDestination;
            if (cc != null) cc.enabled = true;
        }
        // 移動先がない場合は何もしません（その場に留まります）。
    }

    // 現在地から移動可能な「隣接マス（4方向）」をリストで返します。
    // (壁、障害物、他の敵がいるマスは除外します)
    List<Vector3> GetPossibleMoveDestinations()
    {
        List<Vector3> destinations = new List<Vector3>();
        // チェックする4方向のベクトル
        Vector3[] directions = {
            new Vector3(1, 0, 0), new Vector3(-1, 0, 0),
            new Vector3(0, 0, 1), new Vector3(0, 0, -1)
        };

        foreach (var dir in directions)
        {
            Vector3 targetPosition = transform.position + dir;

            // マップ座標に変換
            int mapX = Mathf.RoundToInt(targetPosition.x + 7.5f);
            int mapZ = Mathf.RoundToInt(targetPosition.z + 7.5f);

            // マップ範囲外チェック
            if (mapX < 0 || mapX >= 16 || mapZ < 0 || mapZ >= 16) { continue; }

            // マップチップの種類(移動可能か)チェック (1, 2, 5 は移動不可と仮定)
            int targetCellType = MapGenerator.map[mapZ, mapX];
            if (targetCellType == 1 || targetCellType == 2 || targetCellType == 5) { continue; }

            // 他の敵がいないかチェック
            if (IsEnemyAt(targetPosition)) { continue; }

            // すべてのチェックをパスした場合、移動先としてリストに追加
            destinations.Add(targetPosition);
        }
        return destinations;
    }

    // 指定した座標に (自分以外の) "Enemy" タグを持つオブジェクトがあるかチェックします。
    bool IsEnemyAt(Vector3 position)
    {
        // 指定座標を中心に半径0.4fの球を描画し、接触したすべてのコライダーを取得します。
        Collider[] hitColliders = Physics.OverlapSphere(position, 0.4f);
        foreach (var hitCollider in hitColliders)
        {
            // 接触したオブジェクトが "Enemy" タグを持ち、かつ自分自身でないか確認
            if (hitCollider.CompareTag("Enemy") && hitCollider.gameObject != this.gameObject)
            {
                return true; // 他の敵がいた
            }
        }
        return false; // 他の敵はいなかった
    }
}