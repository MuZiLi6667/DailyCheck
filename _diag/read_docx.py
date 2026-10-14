"""把 .docx 里的文字提取成纯文本（docx 本质是个 zip，正文在 word/document.xml）"""
import re
import sys
import zipfile


def extract(path: str) -> str:
    with zipfile.ZipFile(path) as z:
        xml = z.read("word/document.xml").decode("utf-8")

    # 段落、表格单元格、表格行结束都当成换行/制表符
    xml = xml.replace("</w:p>", "\n")
    xml = xml.replace("</w:tc>", "\t")
    xml = xml.replace("</w:tr>", "\n")

    text = re.sub(r"<[^>]+>", "", xml)
    for a, b in [("&amp;", "&"), ("&lt;", "<"), ("&gt;", ">"),
                 ("&quot;", '"'), ("&apos;", "'")]:
        text = text.replace(a, b)

    # 连续空行压成一个
    out, blank = [], 0
    for line in (l.rstrip() for l in text.split("\n")):
        if not line.strip():
            blank += 1
            if blank > 1:
                continue
        else:
            blank = 0
        out.append(line)
    return "\n".join(out)


if __name__ == "__main__":
    src = sys.argv[1]
    dst = sys.argv[2] if len(sys.argv) > 2 else None
    body = extract(src)
    if dst:
        with open(dst, "w", encoding="utf-8") as f:
            f.write(body)
        print(f"OK: {len(body)} 字符 -> {dst}")
    else:
        sys.stdout.reconfigure(encoding="utf-8")
        print(body)
