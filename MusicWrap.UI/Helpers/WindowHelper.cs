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
