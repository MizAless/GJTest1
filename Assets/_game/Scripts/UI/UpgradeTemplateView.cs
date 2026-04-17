using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UpgradeTemplateView : MonoBehaviour
{
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _description;
    [SerializeField] private TMP_Text _rarity;
    
    [SerializeField] private Button _button;
    
    public void SetUpgrade(string title, string description, string rarity)
    {
        _title.text = title;
        _description.text = description;
        _rarity.text = rarity;
    }
}