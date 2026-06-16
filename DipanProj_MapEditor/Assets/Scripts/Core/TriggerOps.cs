using UnityEngine;
using DipanMapEditor.Data;

namespace DipanMapEditor.Core
{
    /// <summary>Trigger 區域的格操作與顏色解析。</summary>
    public static class TriggerOps
    {
        public static bool HasCell(TriggerRegion r, int x, int y)
        {
            foreach (var c in r.cells)
                if (c != null && c.Length >= 2 && c[0] == x && c[1] == y) return true;
            return false;
        }

        public static void AddCell(TriggerRegion r, int x, int y)
        {
            if (!HasCell(r, x, y)) r.cells.Add(new[] { x, y });
        }

        public static void RemoveCell(TriggerRegion r, int x, int y)
        {
            r.cells.RemoveAll(c => c != null && c.Length >= 2 && c[0] == x && c[1] == y);
        }

        public static Color ParseColor(string hex, float alpha)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out var c))
            {
                c.a = alpha;
                return c;
            }
            return new Color(1f, 1f, 1f, alpha);
        }
    }
}
