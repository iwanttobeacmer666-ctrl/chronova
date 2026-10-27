"""Fetch vetted major-city coordinates from Open-Meteo/GeoNames. No dependencies."""
import concurrent.futures
import json
from pathlib import Path
import urllib.parse
import urllib.request
import sys

groups = {
    "CN": [("Beijing", "北京"), ("Shanghai", "上海"), ("Guangzhou", "广州"), ("Shenzhen", "深圳"), ("Chengdu", "成都"), ("Hangzhou", "杭州"), ("Wuhan", "武汉"), ("Chongqing", "重庆")],
    "US": [("New York", "纽约"), ("Los Angeles", "洛杉矶"), ("Chicago", "芝加哥"), ("San Francisco", "旧金山"), ("Washington", "华盛顿")],
    "GB": [("London", "伦敦"), ("Manchester", "曼彻斯特")],
    "CA": [("Toronto", "多伦多"), ("Vancouver", "温哥华")],
    "AU": [("Sydney", "悉尼"), ("Melbourne", "墨尔本")],
    "JP": [("Tokyo", "东京"), ("Osaka", "大阪")],
    "KR": [("Seoul", "首尔")], "FR": [("Paris", "巴黎")],
    "DE": [("Berlin", "柏林"), ("Munich", "慕尼黑")],
    "IT": [("Rome", "罗马"), ("Milan", "米兰")],
    "ES": [("Madrid", "马德里"), ("Barcelona", "巴塞罗那")],
    "PT": [("Lisbon", "里斯本")], "RU": [("Moscow", "莫斯科"), ("Saint Petersburg", "圣彼得堡")],
    "IN": [("New Delhi", "新德里"), ("Mumbai", "孟买")],
    "BR": [("Brasilia", "巴西利亚"), ("Sao Paulo", "圣保罗")],
    "MX": [("Mexico City", "墨西哥城"), ("Tijuana", "蒂华纳")],
    "SG": [("Singapore", "新加坡")], "NZ": [("Auckland", "奥克兰"), ("Wellington", "惠灵顿")],
    "SA": [("Riyadh", "利雅得")], "AE": [("Dubai", "迪拜"), ("Abu Dhabi", "阿布扎比")]
}

def fetch(item):
    country, name, chinese = item
    query = urllib.parse.urlencode({"name": name, "countryCode": country, "count": 5, "language": "en"})
    with urllib.request.urlopen("https://geocoding-api.open-meteo.com/v1/search?" + query, timeout=25) as response:
        data = json.load(response)
    rows = [r for r in data.get("results", []) if r.get("country_code") == country and r.get("feature_code", "").startswith("P")]
    if not rows:
        raise ValueError(f"No confirmed city: {country} {name}")
    row = max(rows, key=lambda r: r.get("population", 0))
    return {"Id": country + "-" + name.replace(" ", ""), "CountryCode": country,
            "Name": name, "ChineseName": chinese, "Latitude": row["latitude"], "Longitude": row["longitude"],
            "TimeZone": row["timezone"], "GeoNamesId": row["id"]}

items = [(country, name, chinese) for country, cities in groups.items() for name, chinese in cities]
if "--queries" in sys.argv:
    (Path(__file__).parent / "city-queries.json").write_text(json.dumps(items, ensure_ascii=False), encoding="utf-8")
    raise SystemExit(0)
with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
    cities = list(pool.map(fetch, items))
target = Path(__file__).resolve().parent.parent / "Cities.json"
target.write_text(json.dumps(cities, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"Verified {len(cities)} cities in {len(groups)} countries; saved to {target}")
