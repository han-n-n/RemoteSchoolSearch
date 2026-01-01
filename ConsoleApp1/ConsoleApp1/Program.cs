using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 1. 讀取資料
string filePath = Path.Combine(app.Environment.ContentRootPath, "App_Data", "偏遠地區國中小.json");
List<School> allSchools = new();

if (File.Exists(filePath))
{
    try
    {
        string json = File.ReadAllText(filePath, Encoding.UTF8);
        allSchools = JsonSerializer.Deserialize<List<School>>(json) ?? new();
    }
    catch { }
}

var countyList = allSchools.Select(s => s.County).Distinct().OrderBy(c => c).ToList();
var yearList = allSchools.Select(s => s.Year).Distinct().OrderByDescending(y => y).ToList();

// 2. 主頁面：包含篩選與排序功能
app.MapGet("/", (string? year, string? county, string? district, string? keyword, string? minS, string? maxS, string? sort, string? order) =>
{
    var query = allSchools.AsEnumerable();

    // --- 篩選邏輯 ---
    if (!string.IsNullOrEmpty(year)) query = query.Where(s => s.Year == year);
    if (!string.IsNullOrEmpty(county)) query = query.Where(s => s.County == county);
    if (!string.IsNullOrEmpty(district)) query = query.Where(s => s.District == district);
    if (!string.IsNullOrEmpty(keyword)) query = query.Where(s => s.Name.Contains(keyword));

    int.TryParse(minS, out int minVal);
    int.TryParse(maxS, out int maxVal);
    if (!string.IsNullOrEmpty(minS) || !string.IsNullOrEmpty(maxS))
    {
        query = query.Where(s => {
            int.TryParse(s.MaleCount, out int m);
            int.TryParse(s.FemaleCount, out int f);
            int total = m + f;
            bool ok = true;
            if (!string.IsNullOrEmpty(minS)) ok &= (total >= minVal);
            if (!string.IsNullOrEmpty(maxS)) ok &= (total <= maxVal);
            return ok;
        });
    }

    // --- 排序邏輯 ---
    // 預設排序為學年度由大到小
    sort ??= "year";
    order ??= "desc";

    if (sort == "year")
        query = (order == "asc") ? query.OrderBy(s => s.Year) : query.OrderByDescending(s => s.Year);
    else if (sort == "county")
        query = (order == "asc") ? query.OrderBy(s => s.County) : query.OrderByDescending(s => s.County);
    else if (sort == "students")
        query = (order == "asc")
            ? query.OrderBy(s => int.Parse(s.MaleCount) + int.Parse(s.FemaleCount))
            : query.OrderByDescending(s => int.Parse(s.MaleCount) + int.Parse(s.FemaleCount));

    var results = query.ToList();

    // 3. 產生 HTML 內容
    string yearOptions = string.Join("", yearList.Select(y => $"<option value='{y}' {(y == year ? "selected" : "")}>{y}年度</option>"));
    string countyOptions = string.Join("", countyList.Select(c => $"<option value='{c}' {(c == county ? "selected" : "")}>{c}</option>"));

    var filteredDistricts = string.IsNullOrEmpty(county) ? new List<string>() : allSchools.Where(s => s.County == county).Select(s => s.District).Distinct().OrderBy(d => d).ToList();
    string districtOptions = "<option value=''>-- 全部鄉鎮 --</option>" + string.Join("", filteredDistricts.Select(d => $"<option value='{d}' {(d == district ? "selected" : "")}>{d}</option>"));

    // 排序選單 HTML
    var sortOptions = new Dictionary<string, string> {
        { "year_desc", "學年度 (大→小)" }, { "year_asc", "學年度 (小→大)" },
        { "county_asc", "縣市 (A→Z)" }, { "county_desc", "縣市 (Z→A)" },
        { "students_desc", "總人數 (多→少)" }, { "students_asc", "總人數 (少→多)" }
    };
    string sortHtml = string.Join("", sortOptions.Select(opt => $"<option value='{opt.Key}' {(sort + "_" + order == opt.Key ? "selected" : "")}>{opt.Value}</option>"));

    string rows = string.Join("", results.Take(100).Select(s => {
        int.TryParse(s.MaleCount, out int m);
        int.TryParse(s.FemaleCount, out int f);
        return $@"<tr><td>{s.Year}</td><td>{s.County}</td><td>{s.District}</td><td>{s.Name}</td><td>{m + f}人</td><td><a href='/details/{s.Code}'>詳細</a></td></tr>";
    }));

    return Results.Content($@"
        <html>
        <head><meta charset='utf-8'><title>進階查詢系統</title>
        <style>
            body {{ font-family: 'Microsoft JhengHei', sans-serif; padding: 20px; background: #f4f7f6; }}
            .container {{ max-width: 1100px; margin: auto; background: white; padding: 30px; border-radius: 15px; box-shadow: 0 4px 15px rgba(0,0,0,0.1); }}
            .filter-grid {{ display: grid; grid-template-columns: repeat(4, 1fr); gap: 15px; background: #f8f9fa; padding: 20px; border-radius: 10px; margin-bottom: 20px; border: 1px solid #eee; }}
            .field {{ display: flex; flex-direction: column; gap: 5px; }}
            label {{ font-weight: bold; font-size: 0.85em; color: #555; }}
            select, input {{ padding: 8px; border-radius: 5px; border: 1px solid #ccc; }}
            .btn-search {{ grid-column: span 3; padding: 10px; background: #007bff; color: white; border: none; border-radius: 5px; cursor: pointer; font-weight: bold; }}
            .btn-reset {{ padding: 10px; background: #6c757d; color: white; text-decoration: none; border-radius: 5px; text-align: center; }}
            table {{ width: 100%; border-collapse: collapse; margin-top: 20px; }}
            th {{ background: #007bff; color: white; padding: 12px; text-align: left; }}
            td {{ padding: 12px; border-bottom: 1px solid #eee; }}
            tr:hover {{ background: #f9f9f9; }}
        </style>
        <script>
            function autoSubmit() {{ document.getElementById('searchForm').submit(); }}
            function handleSort(val) {{
                const [s, o] = val.split('_');
                document.getElementsByName('sort')[0].value = s;
                document.getElementsByName('order')[0].value = o;
                autoSubmit();
            }}
        </script>
        </head>
        <body>
            <div class='container'>
                <h1>🏫 偏遠地區學校資料庫</h1>
                <form id='searchForm' method='get'>
                    <input type='hidden' name='sort' value='{sort}'>
                    <input type='hidden' name='order' value='{order}'>
                    <div class='filter-grid'>
                        <div class='field'><label>學年度</label><select name='year' onchange='autoSubmit()'><option value=''>-- 全部 --</option>{yearOptions}</select></div>
                        <div class='field'><label>縣市</label><select name='county' onchange='autoSubmit()'><option value=''>-- 全部 --</option>{countyOptions}</select></div>
                        <div class='field'><label>鄉鎮區</label><select name='district' onchange='autoSubmit()'>{districtOptions}</select></div>
                        <div class='field'><label>排序方式</label><select onchange='handleSort(this.value)'>{sortHtml}</select></div>
                        <div class='field'><label>校名關鍵字</label><input type='text' name='keyword' value='{keyword}'></div>
                        <div class='field'><label>最小人數</label><input type='number' name='minS' value='{minS}'></div>
                        <div class='field'><label>最大人數</label><input type='number' name='maxS' value='{maxS}'></div>
                        <div class='btn-group' style='grid-column: span 4; display:flex; gap:10px;'>
                            <button type='submit' class='btn-search'>搜尋資料</button>
                            <a href='/' class='btn-reset'>清除重置</a>
                        </div>
                    </div>
                </form>
                <p>找到 {results.Count} 筆資料</p>
                <table>
                    <thead><tr><th>年度</th><th>縣市</th><th>鄉鎮</th><th>校名</th><th>總人數</th><th></th></tr></thead>
                    <tbody>{rows}</tbody>
                </table>
            </div>
        </body>
        </html>", "text/html", Encoding.UTF8);
});

// 4. 詳細資料頁面 (新增畢業人數)
app.MapGet("/details/{code}", (string code) =>
{
    var s = allSchools.FirstOrDefault(x => x.Code == code);
    if (s == null) return Results.NotFound();
    int.TryParse(s.MaleCount, out int m);
    int.TryParse(s.FemaleCount, out int f);
    return Results.Content($@"
        <html><body style='font-family:sans-serif; padding:40px; background:#f4f7f6;'>
            <div style='max-width:600px; margin:auto; background:white; padding:30px; border-radius:15px; box-shadow:0 5px 15px rgba(0,0,0,0.1);'>
                <h2 style='color:#007bff; border-bottom:2px solid #007bff; padding-bottom:10px;'>{s.Name}</h2>
                <div style='line-height:2;'>
                    <p><b>學年度：</b>{s.Year}</p>
                    <p><b>縣市鄉鎮：</b>{s.County} {s.District}</p>
                    <p><b>地址：</b>{s.Address}</p>
                    <p><b>電話：</b>{s.Phone}</p>
                    <hr>
                    <p><b>在校總人數：</b>{m + f} 人 (男:{m} / 女:{f})</p>
                    <p style='color: #d9534f; font-weight: bold;'>🎓 上學年畢業人數：</p>
                    <ul>
                        <li>男畢業生：{s.GradMale} 人</li>
                        <li>女畢業生：{s.GradFemale} 人</li>
                        <li>畢業生合計：{(int.TryParse(s.GradMale, out int gm) ? gm : 0) + (int.TryParse(s.GradFemale, out int gf) ? gf : 0)} 人</li>
                    </ul>
                    <hr>
                    <p><b>地區屬性：</b>{s.RegionType}</p>
                    <p><b>原住民比率：</b>{s.IndigRatio}%</p>
                </div>
                <br><a href='javascript:history.back()' style='color:#007bff; text-decoration:none; font-weight:bold;'>← 返回搜尋</a>
            </div>
        </body></html>", "text/html", Encoding.UTF8);
});

app.Run();

// 5. 模型定義
public class School
{
    [JsonPropertyName("學年度")] public string Year { get; set; } = "";
    [JsonPropertyName("縣市名稱")] public string County { get; set; } = "";
    [JsonPropertyName("鄉鎮市區")] public string District { get; set; } = "";
    [JsonPropertyName("學校代碼")] public string Code { get; set; } = "";
    [JsonPropertyName("學校名稱")] public string Name { get; set; } = "";
    [JsonPropertyName("地址")] public string Address { get; set; } = "";
    [JsonPropertyName("電話")] public string Phone { get; set; } = "";
    [JsonPropertyName("男學生數[人]")] public string MaleCount { get; set; } = "";
    [JsonPropertyName("女學生數[人]")] public string FemaleCount { get; set; } = "";
    [JsonPropertyName("地區屬性")] public string RegionType { get; set; } = "";
    [JsonPropertyName("原住民學生比率")] public string IndigRatio { get; set; } = "";
    [JsonPropertyName("上學年男畢業生數[人]")] public string GradMale { get; set; } = "";
    [JsonPropertyName("上學年女畢業生數[人]")] public string GradFemale { get; set; } = "";
}