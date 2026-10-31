using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TooltipView : MonoBehaviour
{
    [SerializeField] private Text _title;
    [SerializeField] private Text _subtitle;
    [SerializeField] private Image _accent;
    [SerializeField] private RectTransform _tags;
    [SerializeField] private RectTransform _tagTemplate;
    [SerializeField] private GameObject _intrinsicSection;
    [SerializeField] private Text _intrinsic;
    [SerializeField] private GameObject _propertiesSection;
    [SerializeField] private Text _properties;
    [SerializeField] private GameObject _triggersSection;
    [SerializeField] private Text _triggers;

    private readonly List<RectTransform> _tagPool = new List<RectTransform>();

    public RectTransform Rect => (RectTransform)transform;

    public void SetContent(TooltipContent content)
    {
        _title.text = content.Title;
        _subtitle.text = content.Subtitle;
        _subtitle.color = content.Accent;
        _subtitle.gameObject.SetActive(!string.IsNullOrWhiteSpace(content.Subtitle));
        _accent.color = content.Accent;
        SetTags(content.Tags);
        SetSection(_intrinsicSection, _intrinsic, content.Intrinsic);
        SetSection(_propertiesSection, _properties, Join(content.Properties));
        SetSection(_triggersSection, _triggers, Join(content.Triggers));
        LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);
        // Tag wrapping changes the preferred height after the first layout pass.
        LayoutRebuilder.ForceRebuildLayoutImmediate(Rect);
    }

    private void SetTags(string[] tags)
    {
        int count = tags == null ? 0 : tags.Length;
        for (int index = 0; index < count; index++)
        {
            if (index == _tagPool.Count)
            {
                RectTransform tag = Instantiate(_tagTemplate, _tags);
                tag.name = "Tag";
                _tagPool.Add(tag);
            }

            RectTransform block = _tagPool[index];
            block.gameObject.SetActive(true);
            Text label = block.GetComponentInChildren<Text>();
            label.text = tags[index];
            LayoutElement layout = block.GetComponent<LayoutElement>();
            layout.preferredWidth = Mathf.Min(300, label.preferredWidth + 18);
            layout.preferredHeight = 26;
        }

        for (int index = count; index < _tagPool.Count; index++)
        {
            _tagPool[index].gameObject.SetActive(false);
        }

        _tags.gameObject.SetActive(count > 0);
    }

    private static string Join(string[] lines)
    {
        return lines == null ? "" : string.Join("\n", lines);
    }

    private static void SetSection(GameObject section, Text label, string text)
    {
        label.text = text;
        section.SetActive(!string.IsNullOrWhiteSpace(text));
    }
}
