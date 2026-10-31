using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TooltipContent
{
    public string Title = "Предмет";
    public string Subtitle;
    public Color Accent = new Color(0.83f, 0.66f, 0.39f);
    public string[] Tags = Array.Empty<string>();
    [TextArea] public string Intrinsic;
    public string[] Properties = Array.Empty<string>();
    public string[] Triggers = Array.Empty<string>();
}

// The current combat model has no separate intrinsic field. Callers may supply it explicitly.
public static class AmmoTooltipContent
{
    public static TooltipContent Create(AmmoItem item, AmmoItem module = null, string intrinsic = null)
    {
        if (item == null)
        {
            return null;
        }

        var tags = new List<string>();
        var properties = new List<string>();
        var triggers = new List<string>();
        AddItem(item, "", tags, properties, triggers);
        if (module != null)
        {
            AddItem(module, "Модуль гнезда: ", tags, properties, triggers);
        }

        return new TooltipContent
        {
            Title = item.Name,
            Subtitle = AmmoItem.RarityName(item.Rarity),
            Accent = AmmoItem.RarityColor(item.Rarity),
            Tags = tags.ToArray(),
            Intrinsic = intrinsic,
            Properties = properties.ToArray(),
            Triggers = triggers.ToArray()
        };
    }

    private static void AddItem(AmmoItem item, string prefix, List<string> tags,
        List<string> properties, List<string> triggers)
    {
        if (item.Properties != null)
        {
            foreach (string tag in item.Properties.Tags().Split(new[] { " · " }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!tags.Contains(tag))
                {
                    tags.Add(tag);
                }
            }

            foreach (string property in item.Properties.Description().Split('\n'))
            {
                properties.Add(prefix + property);
            }
        }

        if (item.Trigger != AmmoTrigger.None)
        {
            triggers.Add(prefix + AmmoItem.TriggerName(item.Trigger));
        }
    }
}
