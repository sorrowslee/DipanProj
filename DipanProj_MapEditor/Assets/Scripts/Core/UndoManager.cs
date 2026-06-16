using System.Collections.Generic;
using DipanMapEditor.IO;

namespace DipanMapEditor.Core
{
    /// <summary>
    /// 以「動作前整張地圖快照（JSON）」實作的 Undo。
    /// 每個可復原動作開始前呼叫 Push()；Cmd/Ctrl+Z 時 Undo() 還原最近一筆。
    /// </summary>
    public static class UndoManager
    {
        const int Max = 80;
        static readonly List<string> _stack = new List<string>();

        public static void Push()
        {
            var s = MapSession.Instance;
            if (s == null || s.Map == null) return;
            _stack.Add(JsonConfig.Serialize(s.Map));
            if (_stack.Count > Max) _stack.RemoveAt(0);
        }

        public static void Undo()
        {
            if (_stack.Count == 0) return;
            int i = _stack.Count - 1;
            string json = _stack[i];
            _stack.RemoveAt(i);
            MapSession.Instance?.RestoreFromJson(json);
        }

        public static void Clear() => _stack.Clear();
        public static int Count => _stack.Count;
    }
}
