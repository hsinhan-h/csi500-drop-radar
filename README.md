# 指數漲跌幅分析工具

分析多市場指數成分股在指定時間區間內的漲跌幅，支援**跌幅排行**與**漲幅排行**兩種模式，並可匯出 Excel 報表。

**線上版本**：https://index-swing-radar.onrender.com/

---

## 功能

- 支援多個市場指數：**中證500（CSI 500）**、**費城半導體（SOX）**、**納斯達克100（NDX）**
- 可切換**跌幅排行**或**漲幅排行**模式
- 查詢**近一週**或**近一個月**的成分股漲跌幅
- 即時顯示抓取進度（非同步後台任務）
- 列出自訂筆數的排行（前 5 / 10 / 20 / 50 名）
- 一鍵下載 Excel 報表（含排行與全部成分股兩個 Sheet）
- 響應式網頁（RWD），支援手機與桌機瀏覽
- 支援 PWA 安裝，可加入手機主畫面獨立執行（Android / iOS）
- 支援繁中 / English 切換（i18n）

## 技術架構

| 層級 | 技術 |
|------|------|
| 後端框架 | ASP.NET Core 10（Minimal API + Controllers） |
| 資料來源 | 東方財富（CSI500 成分股）、iShares / Nasdaq（SOX / NDX 成分股）、騰訊財經 / Yahoo Finance（個股行情） |
| Excel 匯出 | ClosedXML 0.105 |
| 前端 | 原生 HTML / CSS / JavaScript（無框架依賴） |

## 專案結構

```
csi500-drop-radar/
├── Controllers/
│   └── ApiController.cs              # REST API
├── Models/
│   ├── StockRecord.cs                # 個股資料模型
│   └── TaskState.cs                  # 任務狀態與結果模型
├── Services/
│   ├── DropAnalysisService.cs        # 排行分析（漲幅 / 跌幅）
│   ├── TaskManagerService.cs         # 後台任務管理
│   ├── Indices/                      # 多指數 SOLID 模組架構
│   │   ├── Csi500/                   # 中證500 模組
│   │   ├── Sox/                      # 費城半導體 模組
│   │   ├── Ndx/                      # 納斯達克100 模組
│   │   └── UsCommon/                 # 美股共用（Yahoo 行情、交易日曆）
│   └── Localization/
│       └── LocalizationHelper.cs     # 後端 i18n（Excel 輸出用）
├── wwwroot/
│   ├── index.html                    # 前端單頁應用
│   ├── i18n/
│   │   ├── zh-TW.json
│   │   └── en.json
│   └── manifest.json                 # PWA manifest
└── Program.cs
```

## API

### `POST /api/start`
建立查詢任務，立即回傳 `task_id`，實際查詢在背景執行。

**Request Body**
```json
{
  "index":  "csi500",   // "csi500" | "sox" | "ndx"
  "period": "month",    // "week" | "month"
  "mode":   "drop",     // "drop"（跌幅排行）| "rise"（漲幅排行）
  "top_n":  10
}
```

**Response**
```json
{ "task_id": "task_143022123456" }
```

---

### `GET /api/status/{taskId}`
輪詢任務進度與結果。

**Response（完成）**
```json
{
  "status": "done",
  "pct": 100,
  "result": {
    "index_id":     "csi500",
    "period":       "month",
    "mode":         "drop",
    "start_date":   "20250306",
    "end_date":     "20250406",
    "fetched":      498,
    "total":        500,
    "generated_at": "2025-04-06 14:30",
    "top_losers":   [ ... ],
    "all_data":     [ ... ]
  }
}
```

---

### `GET /api/download/{taskId}?top_n=10&lang=zh-TW`
下載 Excel 檔（`.xlsx`）。

- **跌幅模式**：檔名 `XXX跌幅分析_近一個月_2025-04-06.xlsx`
- **漲幅模式**：檔名 `XXX漲幅分析_近一個月_2025-04-06.xlsx`

包含兩個工作表：
- `跌幅前N（區間）` 或 `漲幅前N（區間）` — 排行
- `全部成分股` — 所有有效資料，依漲跌幅排序

---

### `GET /api/indices`
回傳所有可用指數清單（id、名稱、幣別、預估成分股數、預估查詢時間）。

---

## 資料抓取說明

| 指數 | 成分股來源 | 行情來源 |
|------|-----------|---------|
| CSI 500 | 東方財富 Push API（BK0701） | 騰訊財經前複權日 K 線 |
| SOX | iShares SOXX ETF 持倉頁面 | Yahoo Finance |
| NDX | Nasdaq 官方 CSV | Yahoo Finance |

**並行控制**：最多 10 個 worker 同時執行，每次請求間隔 0.2 秒，失敗自動重試最多 3 次。  
**快取機制**：任務結果存於記憶體，**伺服器重啟後遺失**，需重新查詢。

## 執行方式

**前置需求**：.NET 10 SDK

```bash
dotnet run
```

啟動後開啟瀏覽器訪問 `http://localhost:5000`。

## PWA 安裝方式

### Android（Chrome）

1. 用 Chrome 開啟網站網址。
2. 點選右上角選單（三個點）→ **新增到主畫面**。
3. 確認名稱後點 **新增**。

### iOS（Safari）

1. 用 Safari 開啟網站網址。
2. 點選底部 **分享** 按鈕 → **加入主畫面**。
3. 確認名稱後點 **新增**。

---

## 注意事項

- CSI 500 查詢約需 **1–3 分鐘**，SOX / NDX 約 **30–60 秒**，期間請勿關閉頁面。
- 本工具僅供學習與參考用途，**不構成任何投資建議**。
- 資料來源為第三方公開 API，可用性不受本專案控制。
