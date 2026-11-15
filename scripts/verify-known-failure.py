"""Reject missing tests, infrastructure errors, skips, and accidental expiry fixes."""
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
counters = root.find("t:ResultSummary/t:Counters", ns)
assert counters is not None, "Missing test counters"
for key, expected in {"total": "1", "executed": "1", "failed": "1", "passed": "0"}.items():
    assert counters.get(key) == expected, (key, counters.attrib)
results = root.findall("t:Results/t:UnitTestResult", ns)
assert len(results) == 1 and results[0].get("outcome") == "Failed"
assert results[0].get("testName") == "AuthorizationExpiryTests.Abandoned_authorization_should_eventually_expire"
message = results[0].findtext("t:Output/t:ErrorInfo/t:Message", namespaces=ns)
assert message and "Assert.Equal() Failure" in message and "Expected: 0" in message and "Actual:   1" in message, message
print("Verified exactly one real authorization-expiry assertion failure: expected 0, actual 1; no skips.")
