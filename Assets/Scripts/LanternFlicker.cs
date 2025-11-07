using UnityEngine;

// Lightコンポーネントの明るさ(Intensity)を、ランダムかつ滑らかに変化させ、
// ランタンや焚火のような「揺らぎ」を表現します。
public class LanternFlicker : MonoBehaviour
{
    // --- Inspectorウィンドウから調整可能なパラメータ ---

    // 明るさの最小値
    [Tooltip("揺らぎの最小輝度")]
    public float minIntensity = 0.8f;

    // 明るさの最大値
    [Tooltip("揺らぎの最大輝度")]
    public float maxIntensity = 1.2f;

    // 揺らぐ速さ (値が大きいほど速く点滅します)
    [Tooltip("揺らぎの速さ")]
    public float flickerSpeed = 0.5f;

    // --- 内部で使用する変数 ---

    // 制御対象のLightコンポーネント
    private Light lanternLight;

    // Perlinノイズの計算に使用するオフセット値
    // (複数のランタンが全く同じ揺らぎ方をしないようにするため)
    private float randomOffset;

    // ゲーム開始時（またはオブジェクト有効化時）に一度だけ呼ばれます
    void Start()
    {
        // このスクリプトがアタッチされているゲームオブジェクトから、
        // Lightコンポーネントを探して取得します。
        lanternLight = GetComponent<Light>();

        // 揺らぎのパターンが他のオブジェクトと被らないよう、
        // ノイズ計算に使う「シード（種）」となる値をランダムに設定します。
        randomOffset = Random.Range(0f, 65535f);
    }

    // 毎フレーム呼ばれます
    void Update()
    {
        // Perlinノイズ（パーリンノイズ）を使用して、滑らかに変化する値を生成します。
        // PerlinNoiseは、引数が近いと近い値を返すため、カクカクした変化になりません。
        // (Time.time * flickerSpeed) で時間経過と共にノイズが変化するようにします。
        float noise = Mathf.PerlinNoise(randomOffset, Time.time * flickerSpeed);

        // Mathf.Lerp (線形補間) を使い、
        // Perlinノイズで得られた値(0.0～1.0)を、
        // 設定した最小～最大の明るさ(minIntensity～maxIntensity)の範囲に変換（マッピング）します。
        lanternLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }
}