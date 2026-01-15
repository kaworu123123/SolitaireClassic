using UnityEngine;

[CreateAssetMenu(fileName = "ComboVfxProfile", menuName = "VFX/Combo VFX Profile")]
public class ComboVfxProfile : ScriptableObject
{
    [System.Serializable]
    public struct Tier
    {
        [Min(1)] public int minCombo;   // この値以上のコンボで適用
        public Color color;             // スパークの代表色
    }

    [Tooltip("minComboの小さい順に並べてください（例：1,4,7,10...）")]
    public Tier[] tiers = new Tier[]
    {
        new Tier{ minCombo=1,  color=Color.white },
        new Tier{ minCombo=4,  color=new Color(0.5f,1f,1f) },   // シアン寄り
        new Tier{ minCombo=7,  color=new Color(1f,0.9f,0.3f) }, // 黄
        new Tier{ minCombo=10, color=new Color(1f,0.4f,0.8f) }  // マゼンタ寄り
    };

    public Color GetColorForCombo(int combo)
    {
        Color c = Color.white;
        for (int i = 0; i < tiers.Length; i++)
        {
            if (combo >= tiers[i].minCombo) c = tiers[i].color;
            else break;
        }
        return c;
    }
}
