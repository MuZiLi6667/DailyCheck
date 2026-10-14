# -*- coding: utf-8 -*-
"""
按记忆规律重排备考计划。

单词：每天 1 个 Lesson（40 天过完第一轮），每课在 D+1 / D+3 / D+7 / D+15 被复习。
之后三轮复习（每天 2 课）+ 考前快速过（每天 4 课）。
行测：节奏不变，按模块推进。
"""
import json
from datetime import date, timedelta

DATA_FILE = r"D:\打卡程序\app\data\dailycheck.json"

START = date(2026, 9, 12)       # 计划启动日
END = date(2026, 12, 11)        # 考前最后一天
TOTAL = 40                      # 共 40 个 Lesson
EXAM = date(2026, 12, 12)

# 间隔复习的四个时间点
REVIEW_GAPS = [(1, "昨天"), (3, "3 天前"), (7, "7 天前"), (15, "15 天前")]


def ds(d: date) -> str:
    return d.strftime("%Y-%m-%d")


def label(n: int) -> str:
    """第 n 课 -> Unit X Lesson Y"""
    u, l = divmod(n - 1, 4)
    return f"Unit {u + 1} Lesson {l + 1}"


def task(tid: str, text: str, daily: bool = False) -> dict:
    return {"Id": tid, "Text": text, "Done": False, "Daily": daily, "History": {}}


def day_plan(d: date) -> list:
    """某天的单词任务清单"""
    idx = (d - START).days

    # ---------- 第一阶段：学新词（每天 1 课）----------
    if 0 <= idx < TOTAL:
        n = idx + 1
        out = [task(f"n{n}", f"【新学】{label(n)}   (第 {n}/40 课)")]
        for gap, name in REVIEW_GAPS:
            prev = n - gap
            if prev >= 1:
                out.append(task(f"n{prev}r{gap}", f"【复习·{name}】{label(prev)}"))
        return out

    after = idx - TOTAL
    if after < 0:
        return []

    # ---------- 第二轮：10/22 起，每天 2 课（20 天）----------
    if 0 <= after < 20:
        return [task(f"r2_{k}", f"【第二轮】{label(k)}")
                for k in (after * 2 + 1, after * 2 + 2) if k <= TOTAL]

    # ---------- 第三轮：每天 2 课（20 天）----------
    if 20 <= after < 40:
        a = after - 20
        return [task(f"r3_{k}", f"【第三轮】{label(k)}")
                for k in (a * 2 + 1, a * 2 + 2) if k <= TOTAL]

    # ---------- 第四轮：考前快速过，每天 4 课（40 课 ÷ 4 = 正好 10 天）----------
    a = after - 40
    if 0 <= a < 10:
        got = list(range(a * 4 + 1, min(a * 4 + 5, TOTAL + 1)))
        if got:
            return [task(f"r4_{a}", "【第四轮·快速过】" + "、".join(label(k) for k in got))]
    if a == 10:
        # 考前一天，不再安排新内容
        return [task("final_day", "考前最后一天：只看错题本和作文模板，不做新题")]
    return []


def monday(d: date) -> date:
    return d - timedelta(days=d.weekday())


# ============================================================
#  行测按周的重点（索引 0 = 9/7 那一周）
# ============================================================
XC = [
    ["行测：图形推理先做 60 题找感觉"],
    ["行测：图形推理剩余 124 题", "行测：类比推理 139 题"],
    ["行测：定义判断 173 题", "行测：逻辑判断 197 题"],
    ["行测：资料分析 365 题"],
    ["行测：逻辑填空 259 题"],
    ["行测：片段阅读 307 题"],
    ["行测：数量关系 247 题（难题先跳过，保证简单题拿分）"],
    ["行测：错题重做 —— 图形推理 + 类比推理 + 定义判断"],
    ["行测：错题重做 —— 逻辑判断 + 资料分析"],
    ["行测：错题重做 —— 逻辑填空 + 片段阅读 + 数量关系"],
    ["行测：每周 3 套套题，卡时间做 + 错题全搞懂"],
    ["行测：3 套套题 + 薄弱模块专项突破"],
    ["行测：错题本过一遍 + 2 套套题保持手感"],
    ["行测：每天半套保持手感，不追难度"],
]

WEEK_TITLE = [
    "起步：单词从 Unit 1 开始，行测从图形推理开始",
    "单词 Unit 2-3；行测拿下图形推理 + 类比推理",
    "单词 Unit 4-5；行测拿下定义判断 + 逻辑判断",
    "单词 Unit 6-7；行测主攻资料分析",
    "单词 Unit 8；行测主攻逻辑填空",
    "单词 Unit 9；行测主攻片段阅读",
    "单词第一轮收尾（10/21 完成 40 课）",
    "单词第二轮启动；行测转入错题重做",
    "单词第二轮收尾；行测错题重做（逻辑判断+资料分析）",
    "单词第三轮启动；行测错题重做（逻辑填空+片段+数量）",
    "单词第三轮收尾；英语进入全真模考节奏",
    "单词第四轮；全真模考 + 查漏补缺",
    "考前两周：只做真题和错题，不碰新资料",
    "考前最后一周：只看错题和模板，调好作息",
]

# ============================================================
with open(DATA_FILE, encoding="utf-8") as f:
    data = json.load(f)

# ---------- 1. 每日固定任务（单词已移到每天的具体清单里）----------
data["Overall"]["Tasks"] = [
    task("d03", "仔细阅读 1 篇（20 分钟做题 + 40 分钟精析，划主谓宾）", True),
    task("d04", "长篇匹配 1 篇（限时 15 分钟 + 整理同义替换）", True),
    task("d05", "听力 Section C 精听 1 篇（盲听→跟读→听写→记词）", True),
    task("d06", "写译 1 篇（一三五默写模板，二四六翻译）", True),
    task("d07", "睡前 10 分钟复盘生词", True),
    task("d08", "行测刷题（题量见本周目标）", True),
]
for t in data["Overall"]["Tasks"]:
    t["StartDate"] = ds(START)

# ---------- 2. 每天的单词安排 ----------
days = data.setdefault("Days", {})
d = START
while d <= END:
    key = ds(d)
    planned = day_plan(d)
    if planned:
        rec = days.get(key)
        if rec is None:
            rec = {"Date": key, "Title": "", "Tasks": [],
                   "Children": [], "IsExpanded": True}
            days[key] = rec

        keep = [t for t in rec.get("Tasks", [])
                if t.get("Id") not in {p["Id"] for p in planned}]
        rec["Tasks"] = planned + keep

        if not rec.get("Title"):
            n_new = len([p for p in planned if "新学" in p["Text"]])
            n_rev = len(planned) - n_new
            if n_new and n_rev:
                rec["Title"] = f"新学 1 课 + 复习 {n_rev} 课"
            elif n_new:
                rec["Title"] = "新学 1 课"
            else:
                rec["Title"] = f"复习 {n_rev} 课"
    d += timedelta(days=1)

# ---------- 3. 周目标 ----------
week_index = {}
m = monday(START)
i = 0
while m <= END:
    week_index[ds(m)] = i
    m += timedelta(days=7)
    i += 1

# 每周学了哪几课（第一轮阶段）
week_lessons = {}
d = START
while d <= START + timedelta(days=TOTAL - 1):
    n = (d - START).days + 1
    week_lessons.setdefault(ds(monday(d)), []).append(n)
    d += timedelta(days=1)

# 每周复习了哪几课（第二、三轮）
week_review = {}
d = START + timedelta(days=TOTAL)
while d <= END:
    wi = ds(monday(d))
    for t in day_plan(d):
        week_review.setdefault(wi, set()).add(t["Text"])
    d += timedelta(days=1)

weeks = {}
for key, idx in week_index.items():
    items = []
    if key in week_lessons:
        ls = week_lessons[key]
        rng = f"{label(ls[0])} ~ {label(ls[-1])}" if len(ls) > 1 else label(ls[0])
        items.append(task(f"wk{idx}", f"单词：完成 {rng}（共 {len(ls)} 课）"))
    if key in week_review:
        items.append(task(f"wr{idx}", f"单词：复习 {len(week_review[key])} 课（见每天卡片）"))
    for j, x in enumerate(XC[idx] if idx < len(XC) else []):
        items.append(task(f"wx{idx}_{j}", x))

    # 周日模考（10 月起加，考试那周不再模考）
    if 3 <= idx <= 12:
        items.append(task(f"wm{idx}", "周日：完整模考 1 套（9:00-11:20，纸质卷）"))
    if idx == 13:
        items.append(task("wd_final", "12/11 晚：检查证件、文具、耳机，10 点前睡觉"))

    weeks[key] = {
        "Title": WEEK_TITLE[idx] if idx < len(WEEK_TITLE) else "",
        "Tasks": items,
        "Children": [],
        "IsExpanded": True,
    }

data["WeekGoals"] = weeks

# ---------- 4. 月目标里的单词口径同步 ----------
mg = data.get("MonthGoals", {})
if "2026-09" in mg:
    mg["2026-09"]["Tasks"] = [
        task("m91", "单词：完成第 1-19 课（到 Unit 5 Lesson 3）"),
        task("m92", "行测：图形推理 184 + 类比推理 139 + 定义判断 173 + 逻辑判断 197 = 693 题"),
        task("m93", "英语：仔细阅读定位法练熟，每道题能说出定位句在哪"),
        task("m94", "英语：作文模板背下 1-2 个并能默写"),
    ]
if "2026-10" in mg:
    mg["2026-10"]["Tasks"] = [
        task("m101", "单词：10/21 完成第一轮 40 课，随后开始第二轮"),
        task("m102", "行测：资料分析 365 + 逻辑填空 259 + 片段阅读 307 + 数量关系 247 = 1178 题"),
        task("m103", "英语：仔细阅读正确率稳定 6/10，单篇 20 分钟内"),
        task("m104", "英语：作文 30 分钟写出 120 词以上，翻译主谓宾不出错"),
        task("m105", "英语：每周日完整模考 1 套，共 4 套"),
    ]
if "2026-11" in mg:
    mg["2026-11"]["Tasks"] = [
        task("m111", "单词：第三轮 + 第四轮快速过，整理顽固词"),
        task("m112", "行测：所有错题重做一遍，薄弱模块专项突破"),
        task("m113", "英语：模考总分连续 3 次 ≥410 分"),
        task("m114", "英语：听力 Section C 稳定 5/10，作文模板条件反射能写"),
        task("m115", "行测：每周 3 套套题，卡时间做"),
    ]
if "2026-12" in mg:
    mg["2026-12"]["Tasks"] = [
        task("m121", "单词：只过顽固词和真题生词，不背新词"),
        task("m122", "英语：作文模板 5 分钟内默写出框架"),
        task("m123", "英语：所有真题错题看过 2 遍以上"),
        task("m124", "行测：每天半套保持手感，不追新题"),
        task("m125", "12/11 晚：准备证件文具，早睡"),
    ]

# ---------- 5. 总目标 ----------
data["Overall"]["Title"] = f"{EXAM.month}月{EXAM.day}日四级过线（430+），行测刷题同步到位"
data["FirstUseDate"] = ds(START)

with open(DATA_FILE, "w", encoding="utf-8") as f:
    json.dump(data, f, ensure_ascii=False, indent=2)

# ---------- 汇报 ----------
learned = sum(1 for d in days.values() if any("新学" in t["Text"] for t in d["Tasks"]))
print(f"OK 已写入 {DATA_FILE}")
print(f"  - 生成 {len(days)} 天的每日清单")
print(f"  - 第一轮 {TOTAL} 课，{START} ~ {START + timedelta(days=TOTAL - 1)}")
print(f"  - 周目标 {len(weeks)} 条")
