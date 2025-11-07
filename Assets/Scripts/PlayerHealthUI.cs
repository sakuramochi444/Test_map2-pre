// PlayerHealthUI.cs (Update() を使う版)
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class PlayerHealthUI : MonoBehaviour
{
    private Slider healthSlider;

    // インスペクタからPlayerのCharacterStatsを設定する必要がある
    public CharacterStats playerStats;

    void Awake()
    {
        healthSlider = GetComponent<Slider>();
    }

    void Start()
    {
        if (playerStats != null)
        {
            // 最大値の初期設定
            healthSlider.maxValue = playerStats.maxHealth;
        }
    }

    void Update()
    {
        if (playerStats != null)
        {
            // 毎フレーム値を同期する
            healthSlider.value = playerStats.currentHealth;
        }
        else
        {
            // Playerを見失ったら探す (シーンロード時など)
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerStats = playerObj.GetComponent<CharacterStats>();
                if (playerStats != null)
                {
                    healthSlider.maxValue = playerStats.maxHealth;
                }
            }
        }
    }
}