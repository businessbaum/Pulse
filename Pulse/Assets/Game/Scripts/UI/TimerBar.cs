using UnityEngine;
using UnityEngine.UIElements;

namespace Pulse
{
    // A labelled, framed progress bar for things that take a moment, such as entering a dungeon.
    // It sits where subtitles would be: bottom centre, above everything else there.
    public sealed class TimerBar : VisualElement
    {
        readonly Label label;
        readonly VisualElement fill;

        public TimerBar()
        {
            AddToClassList("timer");
            label = new Label();
            label.AddToClassList("timer__label");
            Add(label);
            var frame = new VisualElement();
            frame.AddToClassList("timer__bar");
            Add(frame);
            fill = new VisualElement();
            fill.AddToClassList("timer__fill");
            frame.Add(fill);
            Hide();
        }

        public void Show(string text, float progress)
        {
            style.display = DisplayStyle.Flex;
            label.text = text;
            fill.style.width = Length.Percent(100f * Mathf.Clamp01(progress));
        }

        public void Hide() => style.display = DisplayStyle.None;
    }
}
