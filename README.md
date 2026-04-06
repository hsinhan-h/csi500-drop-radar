# 中證500 跌幅分析工具 (CSI 500 Drop Radar)

分析中證500成分股在指定時間區間內的漲跌幅，找出跌幅最大的個股，並支援匯出 Excel 報表。

---

## 功能

- 查詢**近一週**或**近一個月**的中證500成分股漲跌幅
- 即時顯示抓取進度（非同步後台任務，支援 500 檔同時查詢）
- 列出自訂筆數的跌幅排行（前 5 / 10 / 20 / 50 名）
- 一鍵下載 Excel 報表（含跌幅排行與全部成分股兩個 Sheet）
- 響應式網頁（RWD），支援手機與桌機瀏覽

## 技術架構

| 層級 | 技術 |
|------|------|
| 後端框架 | ASP.NET Core 10（Minimal API + Controllers） |
| 資料來源 | 東方財富（成分股清單）、騰訊財經（個股歷史行情） |
| Excel 匯出 | ClosedXML 0.105 |
| 前端 | 原生 HTML / CSS / JavaScript（無框架依賴） |

## 專案結構

```
csi500-drop-radar/
├── Controllers/
│   └── ApiController.cs       # REST API（/api/start、/api/status、/api/download）
├── Models/
│   ├── StockRecord.cs         # 個股資料模型
│   └── TaskState.cs           # 任務狀態與結果模型
├── Services/
│   ├── StockFetchService.cs   # 成分股清單與個股行情抓取邏輯
│   └── TaskManagerService.cs  # 後台任務管理（in-memory）
├── wwwroot/
│   └── index.html             # 前端單頁應用
└── Program.cs                 # 服務注入與中介層設定
```

## API

### `POST /api/start`
建立一個新的查詢任務，立即回傳 `task_id`，實際查詢在背景執行。

**Request Body**
```json
{
  "period": "month",   // "week" 或 "month"
  "top_n": 10          // 回傳跌幅前 N 名，預設 10
}
```

**Response**
```json
{ "task_id": "task_143022123456" }
```

---

### `GET /api/status/{taskId}`
輪詢任務進度與結果。

**Response（進行中）**
```json
{
  "status": "running",
  "progress": "抓取進度：250/500，成功 248 檔...",
  "pct": 50
}
```

**Response（完成）**
```json
{
  "status": "done",
  "pct": 100,
  "result": {
    "period_label": "近一個月",
    "start_date": "20250306",
    "end_date": "20250406",
    "fetched": 498,
    "total": 500,
    "generated_at": "2025-04-06 14:30",
    "top_losers": [ ... ],
    "all_data": [ ... ]
  }
}
```

---

### `GET /api/download/{taskId}`
下載 Excel 檔（`.xlsx`），包含兩個工作表：
- `跌幅前N（區間）` — 跌幅排行
- `全部成分股` — 所有有效資料，依跌幅升冪排序

---

## 資料抓取說明

1. **成分股清單**：呼叫東方財富 Push API（`BK0701`），翻頁抓取，每頁 100 筆。
2. **個股歷史行情**：呼叫騰訊財經前複權日 K 線 API，取期間首尾交易日收盤價計算漲跌幅。
3. **並行控制**：最多 10 個 worker 同時執行，每次請求間隔 0.2 秒，失敗自動重試最多 3 次。
4. **快取機制**：任務結果存於記憶體，**伺服器重啟後遺失**，需重新查詢。

## 執行方式

**前置需求**：.NET 10 SDK

```bash
# 還原套件並啟動
dotnet run
```

啟動後開啟瀏覽器訪問 `http://localhost:5000`（或 launchSettings.json 設定的埠號）。

## 注意事項

- 查詢 500 檔約需 **1–3 分鐘**，期間請勿關閉頁面。
- 本工具僅供學習與參考用途，**不構成任何投資建議**。
- 資料來源為第三方公開 API，可用性不受本專案控制。
