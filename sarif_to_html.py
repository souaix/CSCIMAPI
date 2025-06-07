
import json
import sys
from pathlib import Path
from bs4 import BeautifulSoup

def generate_html_report(sarif_data):
    runs = sarif_data.get("runs", [])
    results = runs[0].get("results", []) if runs else []

    html = BeautifulSoup("<html><head><title>DevSkim Report</title></head><body><h1>Security Scan Report</h1></body></html>", "html.parser")
    body = html.body

    if not results:
        body.append(html.new_tag("p"))
        body.p.string = "No issues found."
        return str(html)

    table = html.new_tag("table", border="1", cellspacing="0", cellpadding="5")
    header = html.new_tag("tr")
    for col in ["Rule ID", "Message", "Severity", "File", "Start Line"]:
        th = html.new_tag("th")
        th.string = col
        header.append(th)
    table.append(header)

    for result in results:
        rule_id = result.get("ruleId", "N/A")
        message = result.get("message", {}).get("text", "")
        level = result.get("level", "N/A")
        location = result.get("locations", [{}])[0].get("physicalLocation", {})
        file_path = location.get("artifactLocation", {}).get("uri", "N/A")
        start_line = location.get("region", {}).get("startLine", "N/A")

        row = html.new_tag("tr")
        for value in [rule_id, message, level, file_path, str(start_line)]:
            td = html.new_tag("td")
            td.string = value
            row.append(td)
        table.append(row)

    body.append(table)
    return str(html)

def main():
    if len(sys.argv) < 3:
        print("Usage: python sarif_to_html.py input.sarif output.html")
        sys.exit(1)

    input_path = Path(sys.argv[1])
    output_path = Path(sys.argv[2])

    if not input_path.exists():
        print(f"Input file not found: {input_path}")
        sys.exit(1)

    with input_path.open("r", encoding="utf-8") as f:
        sarif_data = json.load(f)

    html_content = generate_html_report(sarif_data)

    with output_path.open("w", encoding="utf-8") as f:
        f.write(html_content)

    print(f"HTML report saved to: {output_path}")

if __name__ == "__main__":
    main()
