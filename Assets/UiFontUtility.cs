using UnityEngine;

public static class UiFontUtility
{
    private static Font defaultFont;

    public static Font DefaultFont
    {
        get
        {
            if (defaultFont != null)
            {
                return defaultFont;
            }

            defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            if (defaultFont == null)
            {
                defaultFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            if (defaultFont == null)
            {
                defaultFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
            }

            return defaultFont;
        }
    }
}
