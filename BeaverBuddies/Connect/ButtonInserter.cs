using System;
using System.Collections.Generic;
using System.Text;
using Timberborn.CoreUI;
using UnityEngine.UIElements;

namespace BeaverBuddies.Connect
{
    internal static class ButtonInserter
    {
        public static Button DuplicateButton(Button previousButton)
        {
            var classList = previousButton.classList;
            Button button = new LocalizableButton();
            button.classList.AddRange(classList);
            var parent = previousButton.parent;
            var index = parent.IndexOf(previousButton);
            previousButton.parent.Insert(index + 1, button);
            return button;
        }
    }
}
