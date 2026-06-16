using UnityEngine;

namespace DipanMapEditor.Core
{
    /// <summary>Cmd+Z（Mac）／Ctrl+Z（Win）→ Undo。</summary>
    public class UndoHotkey : MonoBehaviour
    {
        void Update()
        {
            bool mod = Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand)
                    || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (mod && Input.GetKeyDown(KeyCode.Z))
                UndoManager.Undo();
        }
    }
}
