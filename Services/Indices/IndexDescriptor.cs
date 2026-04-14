namespace Csi500DropRadar.Services.Indices;

/// <summary>描述一個指數的靜態元資訊，用於 /api/indices 回傳與 Excel 命名。</summary>
public record IndexDescriptor(
    string Id,                       // "csi500" | "sox" | "ndx"
    string DisplayNameZh,            // "中證500"
    string DisplayNameEn,            // "CSI 500"
    string Currency,                 // "CNY" | "USD"
    int ExpectedConstituentCount,    // 估計成分股數，供 UI 顯示耗時提示
    string EstimatedTimeZh,          // "約需 1–3 分鐘"
    string EstimatedTimeEn           // "~1–3 min"
);
