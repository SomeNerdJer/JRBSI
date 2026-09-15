using System.Windows;

namespace JRBSI;

public partial class App : Application
{
    public static bool IsDarkTheme { get; private set; } = true;

    public static void SetDarkTheme(bool dark)
    {
        IsDarkTheme = dark;

        var merged = Current.Resources.MergedDictionaries;
        var theme = new ResourceDictionary
        {
            Source = new Uri(dark ? "Themes/Dark.xaml" : "Themes/Light.xaml", UriKind.Relative)
        };

        for (var i = 0; i < merged.Count; i++)
        {
            var source = merged[i].Source?.OriginalString ?? string.Empty;
            if (source.Contains("Themes/", StringComparison.OrdinalIgnoreCase))
            {
                merged[i] = theme;
                return;
            }
        }

        merged.Add(theme);
    }
}
