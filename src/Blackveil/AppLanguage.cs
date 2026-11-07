using System.Globalization;

namespace Blackveil;

internal static class AppLanguage
{
    internal static void Initialize()
    {
        // Keep dates, local time and keyboard layout in the user's regional format.
        // Only the UI language is fixed, including helper and background threads.
        var english = CultureInfo.GetCultureInfo("en-US");
        CultureInfo.DefaultThreadCurrentUICulture = english;
        CultureInfo.CurrentUICulture = english;
    }
}
