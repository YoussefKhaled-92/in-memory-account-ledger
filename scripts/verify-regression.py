"""Ensure CI executes every migrated regression case without skips."""
import sys
import xml.etree.ElementTree as ET

ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
root = ET.parse(sys.argv[1]).getroot()
counters = root.find("t:ResultSummary/t:Counters", ns)
assert counters is not None
for key, value in {"total": "52", "executed": "52", "passed": "52", "failed": "0", "notExecuted": "0"}.items():
    assert counters.get(key) == value, (key, counters.attrib)
print("Verified 52 executed, 52 passed, zero failed or skipped regression cases.")
