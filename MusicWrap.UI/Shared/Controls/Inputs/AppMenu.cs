using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace MusicWrap.UI.Controls;

public class AppMenu : Menu
{
    static AppMenu()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(AppMenu),
            new FrameworkPropertyMetadata(typeof(AppMenu))
        );
    }

    //protected override DependencyObject GetContainerForItemOverride()
    //   => new AppMenuItem();

    //protected override bool IsItemItsOwnContainerOverride(object item)
    //    => item is AppMenuItem;

}

public class AppMenuItem : MenuItem
{
    static AppMenuItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(AppMenuItem),
            new FrameworkPropertyMetadata(typeof(AppMenuItem)));
    }
}

