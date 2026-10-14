# -*- coding: utf-8 -*-
"""检查生成的计划是否符合记忆规律"""
import json
import sys

sys.stdout.reconfigure(encoding="utf-8")

d = json.load(open(r"D:\打卡程序\app\data\dailycheck.json", encoding="utf-8"))

print("=== 每日固定任务 ===")
for t in d["Overall"]["Tasks"]:
    print("  -", t["Text"])

print()
print("=== 前 18 天的单词安排（验证间隔复习）===")
keys = sorted(d["Days"].keys())
for k in keys[:18]:
    v = d["Days"][k]
    print(f"{k}")
    for t in v["Tasks"]:
        print("    ", t["Text"])

print()
print("=== 第一轮结束前后（10/19 - 10/24）===")
for k in [x for x in keys if "2026-10-19" <= x <= "2026-10-24"]:
    print(f"{k}")
    for t in d["Days"][k]["Tasks"]:
        print("    ", t["Text"])

print()
print("=== 周目标 ===")
for k, v in d["WeekGoals"].items():
    print(f"{k}  {v['Title']}")
    for t in v["Tasks"]:
        print("     -", t["Text"])

print()
keys_all = sorted(d["Days"].keys())
print(f"总天数：{len(keys_all)}   范围：{keys_all[0]} ~ {keys_all[-1]}")

from datetime import date, timedelta
x, end = date(2026, 9, 12), date(2026, 12, 11)
missing = []
while x <= end:
    if x.strftime("%Y-%m-%d") not in d["Days"]:
        missing.append(x.strftime("%Y-%m-%d"))
    x += timedelta(days=1)
print("缺失的日期：", missing if missing else "无")
print("空任务的日期：", [k for k in keys_all if not d["Days"][k]["Tasks"]])
