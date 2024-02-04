using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace BeaverBuddies.Connect
{
    internal static class ButtonInserter
    {
        public static Button DuplicateOrGetButton(VisualElement root, string previousButtonName, string buttonName, Action<Button> init)
        {
            var button = root.Q<Button>(buttonName);
            if (button != null)
            {
                return button;
            }
            var previousButton = root.Q<Button>(previousButtonName);
            var classList = previousButton.classList;
            button = new LocalizableButton();
            button.name = buttonName;
            button.classList.AddRange(classList);
            var parent = previousButton.parent;
            var index = parent.IndexOf(previousButton);
            previousButton.parent.Insert(index + 1, button);

            init(button);

            return button;
        }
    }
}
