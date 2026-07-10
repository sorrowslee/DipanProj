## Imported Claude Cowork project instructions

燃燈劫遊戲專案

## ChatGPT / Codex 專用規則

- 本節規則只適用於 ChatGPT / Codex，不適用於其他 AI agent；其他 agent 依使用者另外給予的規則行事。
- 使用者在對話中交代給 ChatGPT / Codex 的後續規則，除非使用者明確表示為全專案或全 agent 共用，否則一律只約束 ChatGPT / Codex。
- ChatGPT / Codex 後續進行任何需要修改專案的任務前，統一使用 `feat/gpt` 分支。
- ChatGPT / Codex 開始工作前先確認本地是否已有 `feat/gpt`；若存在，先刪除再重新建立，不沿用上一次任務的分支內容。
- 新的 `feat/gpt` 必須以最新的 `develop` 為起點：先更新 `develop`，再從更新後的 `develop` 建立 `feat/gpt`。
- 刪除／重建分支與更新 `develop` 前，先確認工作樹狀態，避免覆蓋或遺失尚未提交的使用者修改。
- 純閱讀、說明、分析或不修改專案的任務，不必切換或重建分支。
