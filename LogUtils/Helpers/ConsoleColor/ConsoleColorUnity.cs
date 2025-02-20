using _ConsoleColor = System.ConsoleColor;
using UnityEngine;

namespace LogUtils.Helpers.ConsoleColor
{
    public class ConsoleColorUnity
    {
        public static (_ConsoleColor consoleColor, Color unityColor)[] consoleColors =
        [
            (_ConsoleColor.Black,       new Color(0f, 0f, 0f)),
            (_ConsoleColor.DarkBlue,    new Color(0f, 0f, 0.5f)),
            (_ConsoleColor.DarkGreen,   new Color(0f, 0.5f, 0f)),
            (_ConsoleColor.DarkCyan,    new Color(0f, 0.5f, 0.5f)),
            (_ConsoleColor.DarkRed,     new Color(0.5f, 0f, 0f)),
            (_ConsoleColor.DarkMagenta, new Color(0.5f, 0f, 0.5f)),
            (_ConsoleColor.DarkYellow,  new Color(0.5f, 0.5f, 0f)),
            (_ConsoleColor.Gray,        new Color(0.75f, 0.75f, 0.75f)),
            (_ConsoleColor.DarkGray,    new Color(0.5f, 0.5f, 0.5f)),
            (_ConsoleColor.Blue,        new Color(0f, 0f, 1f)),
            (_ConsoleColor.Green,       new Color(0f, 1f, 0f)),
            (_ConsoleColor.Cyan,        new Color(0f, 1f, 1f)),
            (_ConsoleColor.Red,         new Color(1f, 0f, 0f)),
            (_ConsoleColor.Magenta,     new Color(1f, 0f, 1f)),
            (_ConsoleColor.Yellow,      new Color(1f, 1f, 0f)),
            (_ConsoleColor.White,       new Color(1f, 1f, 1f))
        ];

        public static Color GetUnityColor(_ConsoleColor consoleColor)
        {
            foreach (var (color, unityColor) in consoleColors)
            {
                if (color == consoleColor)
                {
                    return unityColor;
                }
            }
            return Color.white;
        }
    }
}
