using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace MusicWrap.UI.Helpers
{
    public class WindowHelper
    {
        public static bool? LauchFromParent(Window parent, Window child, bool isDialog = false)
        {
            if (parent is null || child is null) return null;

            child.Owner = parent;
            child.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            child.Closed += (s, e) =>
            {
                if (child is IDisposable disposable)
                    disposable.Dispose();
                parent.Activate();
            };
            if (isDialog)
            {
                return child.ShowDialog();
            }
            else
            {
                child.Show();
                return null;
            }
        }
    }
}
