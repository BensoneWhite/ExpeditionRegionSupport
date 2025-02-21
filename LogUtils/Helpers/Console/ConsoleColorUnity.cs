using System;
using UnityEngine;

namespace LogUtils.Helpers.Console
{
    public class ConsoleColorUnity
    {
        public static (ConsoleColor consoleColor, Color unityColor)[] consoleColors =
        [
            (ConsoleColor.Black,       new Color(0f, 0f, 0f)),
            (ConsoleColor.DarkBlue,    new Color(0f, 0f, 0.5f)),
            (ConsoleColor.DarkGreen,   new Color(0f, 0.5f, 0f)),
            (ConsoleColor.DarkCyan,    new Color(0f, 0.5f, 0.5f)),
            (ConsoleColor.DarkRed,     new Color(0.5f, 0f, 0f)),
            (ConsoleColor.DarkMagenta, new Color(0.5f, 0f, 0.5f)),
            (ConsoleColor.DarkYellow,  new Color(0.5f, 0.5f, 0f)),
            (ConsoleColor.Gray,        new Color(0.75f, 0.75f, 0.75f)),
            (ConsoleColor.DarkGray,    new Color(0.5f, 0.5f, 0.5f)),
            (ConsoleColor.Blue,        new Color(0f, 0f, 1f)),
            (ConsoleColor.Green,       new Color(0f, 1f, 0f)),
            (ConsoleColor.Cyan,        new Color(0f, 1f, 1f)),
            (ConsoleColor.Red,         new Color(1f, 0f, 0f)),
            (ConsoleColor.Magenta,     new Color(1f, 0f, 1f)),
            (ConsoleColor.Yellow,      new Color(1f, 1f, 0f)),
            (ConsoleColor.White,       new Color(1f, 1f, 1f))
        ];

        public static Color GetUnityColor(ConsoleColor consoleColor)
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
