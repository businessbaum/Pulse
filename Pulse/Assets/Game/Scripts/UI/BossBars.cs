using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pulse
{
    // Hull bars for mini bosses and bosses. Each one carries a small bar over its head, with one
    // notch for a mini boss and two for a boss. A boss that has noticed the machine also gets the
    // wide bar at the top of the screen, with its name.
    public sealed class BossBars
    {
        sealed class Tag
        {
            public VisualElement root, fill, secondNotch;
            public Label name;
        }

        readonly VisualElement layer, top, topFill, topMark;
        readonly Label topName, topRank;
        readonly List<Tag> tags = new List<Tag>();

        // layer holds the floating bars and sits behind the rest of the HUD. screen holds the wide bar.
        public BossBars(VisualElement layer, VisualElement screen)
        {
            this.layer = layer;
            top = Ui.Add(screen, new VisualElement(), "boss");
            var head = Ui.Add(top, new VisualElement(), "boss__head");
            var badge = Ui.Add(head, new VisualElement(), "boss__badge");
            Ui.Add(badge, new Hatch());
            topRank = Ui.Add(badge, new Label(), "boss__rank");
            topName = Ui.Add(head, new Label(), "title");
            var bar = Ui.Add(top, new VisualElement(), "boss__bar");
            topFill = Ui.Add(bar, new VisualElement(), "boss__fill");
            // A Warden's bar has a mark at half: that is where its second phase begins.
            topMark = Ui.Add(bar, new VisualElement(), "boss__mark");
            top.style.display = DisplayStyle.None;
        }

        public void Refresh(Camera camera)
        {
            var elites = Combat.Enemies.Elites;
            int used = 0, lead = -1;
            for (int i = 0; i < elites.Count; i++)
            {
                var elite = elites[i];
                // The nearest boss that is after the machine goes to the top of the screen.
                if (elite.rank >= Rank.Boss && elite.aggro && (lead < 0 || elite.distance < elites[lead].distance)) lead = i;
                if (camera == null || layer.panel == null) continue;

                var tag = used < tags.Count ? tags[used] : NewTag();
                used++;
                tag.root.style.display = DisplayStyle.Flex;
                tag.name.text = elite.name.ToUpperInvariant();
                tag.secondNotch.style.display = elite.rank >= Rank.Boss ? DisplayStyle.Flex : DisplayStyle.None;
                tag.fill.style.width = Length.Percent(100f * elite.hull);
                Vector2 at = RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel, elite.head, camera);
                tag.root.style.left = at.x - tag.root.resolvedStyle.width * 0.5f;
                tag.root.style.top = at.y - tag.root.resolvedStyle.height;
            }
            for (int i = used; i < tags.Count; i++) tags[i].root.style.display = DisplayStyle.None;

            top.style.display = lead >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (lead < 0) return;
            topName.text = elites[lead].name.ToUpperInvariant();
            topRank.text = Ranks.Label(elites[lead].rank).ToUpperInvariant();
            topMark.style.display = elites[lead].rank == Rank.Warden && elites[lead].stage < 2 ? DisplayStyle.Flex : DisplayStyle.None;
            topFill.style.width = Length.Percent(100f * elites[lead].hull);
        }

        Tag NewTag()
        {
            var root = Ui.Add(layer, new VisualElement(), "elite");
            var head = Ui.Add(root, new VisualElement(), "elite__head");
            Ui.Add(head, new VisualElement(), "notch");
            var second = Ui.Add(head, new VisualElement(), "notch");
            var name = Ui.Add(head, new Label(), "elite__name");
            var fill = Ui.Add(Ui.Add(root, new VisualElement(), "elite__bar"), new VisualElement(), "elite__fill");
            var tag = new Tag { root = root, fill = fill, secondNotch = second, name = name };
            tags.Add(tag);
            return tag;
        }
    }
}
