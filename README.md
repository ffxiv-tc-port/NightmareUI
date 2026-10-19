<!-- ffxiv-tc-port 繁體中文說明開始 -->
# NightmareUI(台服 fork)

由 NightmareXIV 開發的 ImGui 介面函式庫，提供世界選單（`WorldSelector`）、可折疊區塊等介面元件，
供 NightmareXIV 系列插件共用。上游標明只供內部使用。

## 台服 fork 的目的

讓世界選單在台服列得出伺服器，並修正可折疊區塊點了不會展開的問題。

## 與上游的差異

- **世界選單支援台服**：台服世界在資料表裡不是「公開」世界，原本整個選單列不出任何世界；
  改為另外放行台服的官方世界，並把台服區域標示為「TW」。
- **可折疊區塊修正**：折疊狀態原本沒寫回 ImGui，點了不會展開。
- **建置設定**：兩條台服分支收斂成同一份，拿掉兩個從來就找不到的組件引用。

## 誰在用它

目前 4 個插件 repo 以子模組使用：`AutoRetainer`、`Lifestream`、`Splatoon`、`TextAdvance`。

---

以下為上游原始 README，內容未經修改：

<!-- ffxiv-tc-port 繁體中文說明結束 -->

# This library is currently for internal use only!
