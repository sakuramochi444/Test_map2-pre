using UnityEngine;
using UnityEngine.UI;

// UIのRawImageコンポーネントに、ゲーム内のマップ情報をピクセルアートとして描画します。
// プレイヤーの位置、探索済みの領域、マップ構造（壁、床など）を表示します。
[RequireComponent(typeof(RawImage))] // このスクリプトは RawImage コンポーネントが必須です
public class MiniMapDisplay : MonoBehaviour
{
    // --- 内部参照 ---
    private RawImage miniMapImage; // 描画先となるUIのRawImageコンポーネント
    private Texture2D miniMapTexture; // ピクセル情報を書き込むためのTexture2Dオブジェクト
    private Transform playerTransform; // プレイヤーの現在位置を追跡するためのTransform

    [Header("マップの色設定")]
    public Color floorColor = new Color(0.5f, 0.5f, 0.5f); // 0: 道
    public Color wallColor = new Color(0.2f, 0.2f, 0.8f); // 1: 壁
    public Color stairsColor = Color.yellow; // 2: 階段
    public Color chestColor = Color.cyan; // 3: 宝箱
    public Color openWallColor = new Color(0.2f, 0.8f, 0.2f); // 5: 開く壁
    public Color playerColor = Color.red; // プレイヤーの現在地
    public Color fogColor = new Color(0.1f, 0.1f, 0.1f, 1.0f); // 未探索エリア

    // マップのサイズ（縦・横のピクセル数）。MapGeneratorの16x16と一致させます。
    private const int mapSize = 16;

    // プレイヤーの検索処理をUpdateで過剰に実行しないためのフラグ
    private bool playerSearched = false;

    // コンポーネントを取得し、ミニマップ用のテクスチャを初期化します。
    void Start()
    {
        miniMapImage = GetComponent<RawImage>();

        // 16x16ピクセルのテクスチャを作成
        miniMapTexture = new Texture2D(mapSize, mapSize);

        // ピクセルがぼやけないように、フィルターモードをPoint（最近傍法）に設定します。
        // これにより、ドット絵がくっきりと表示されます。
        miniMapTexture.filterMode = FilterMode.Point;

        // RawImageコンポーネントが、この作成したテクスチャを表示するように設定
        miniMapImage.texture = miniMapTexture;

        // ゲーム開始時に一度、マップを描画する（データが揃っていれば）
        DrawMap();
    }

    // プレイヤーオブジェクトをシーンから検索します。
    // (シーンロード直後など、参照が失われた場合に使われます)
    void FindPlayer()
    {
        // プレイヤーの参照(playerTransform)がまだ無く、検索処理もまだ実行していない場合
        if (playerTransform == null && !playerSearched)
        {
            // "Player" タグでプレイヤーオブジェクトを検索
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerTransform = playerObj.transform;
            }

            // 検索処理を実行したことを記録します。
            // (見つかったかどうかに関わらず、毎フレーム検索し続けないようにするため)
            playerSearched = true;
        }
    }

    // 毎フレーム実行されます
    void Update()
    {
        // プレイヤーの参照がなければ（例：シーンロード直後など）検索を試みます。
        FindPlayer();

        // 毎フレーム、マップの状態をテクスチャに再描画します。
        // (プレイヤーの移動や探索状況の変化を反映するため)
        DrawMap();
    }

    // 現在のゲーム状態（マップ、探索状況、プレイヤー位置）に基づいてミニマップテクスチャを更新します。
    void DrawMap()
    {
        // 必要なデータ（GameManagerや探索データ、マップデータ）がロードされていない場合は処理を中断します。
        if (GameManager.instance == null || GameManager.instance.exploredMapData == null || MapGenerator.map == null)
        {
            // データがロードされていない場合、テクスチャ全体を「霧」（未探索色）で塗りつぶします。
            ClearTexture(fogColor);
            miniMapTexture.Apply(); // テクスチャの変更を適用
            return;
        }

        // GameManagerから探索済みデータ(bool[,])とマップ構造データ(int[,])を取得
        bool[,] explored = GameManager.instance.exploredMapData;
        int[,] map = MapGenerator.map;

        // プレイヤーのワールド座標をマップ座標（0～15）に変換
        int playerX = -1, playerZ = -1;
        if (playerTransform != null)
        {
            // オフセット(7.5f)を加えて四捨五入することで、ワールド座標を配列インデックスに変換します。
            playerX = Mathf.RoundToInt(playerTransform.position.x + 7.5f);
            playerZ = Mathf.RoundToInt(playerTransform.position.z + 7.5f);
        }

        // マップの全ピクセル（タイル）を(0,0)から(15,15)までループ
        for (int z = 0; z < mapSize; z++)
        {
            for (int x = 0; x < mapSize; x++)
            {
                // 1. 探索済みの場合 (explored[z, x] == true)
                if (explored[z, x])
                {
                    // 1a. 現在の(x, z)がプレイヤーの座標と一致する場合
                    if (x == playerX && z == playerZ)
                    {
                        miniMapTexture.SetPixel(x, z, playerColor); // プレイヤーの色を描画
                    }
                    // 1b. プレイヤーの座標ではない、探索済みのマスの場合
                    else
                    {
                        // GetColorForTileメソッドでマップタイプに応じた色を取得して描画
                        miniMapTexture.SetPixel(x, z, GetColorForTile(map[z, x]));
                    }
                }
                // 2. 未探索の場合 (explored[z, x] == false)
                else
                {
                    miniMapTexture.SetPixel(x, z, fogColor); // 未探索の色（霧）を描画
                }
            }
        }

        // ループですべてのピクセル設定が終わったら、
        // 変更をテクスチャに反映（GPUにアップロード）します。
        // これを呼ばないと表示は変わりません。
        miniMapTexture.Apply();
    }

    // マップデータ（int値）に対応する色を返します。
    private Color GetColorForTile(int tileType)
    {
        switch (tileType)
        {
            case 0: return floorColor;    // 0: 道
            case 1: return wallColor;     // 1: 壁
            case 2: return stairsColor;   // 2: 階段
            case 3: return chestColor;    // 3: 宝箱
            case 4: return floorColor;    // 4: 敵（ミニマップ上は道として表示）
            case 5: return openWallColor; // 5: 開く壁
            default: return floorColor;   // 不明な値（床として表示）
        }
    }

    // テクスチャ全体を指定された色で高速に塗りつぶします。
    // (DrawMapのロード待ち処理で使用)
    private void ClearTexture(Color color)
    {
        if (miniMapTexture == null) return;

        // 全ピクセル数分の色配列を作成
        Color[] pixels = new Color[mapSize * mapSize];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }

        // 配列（pixels）を使って一度に全ピクセルを設定（SetPixelをピクセル数分呼ぶより高速）
        miniMapTexture.SetPixels(pixels);
    }
}