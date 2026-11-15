"""Verify the assessment's 52 passing cases and one genuine design failure."""
import sys
import xml.etree.ElementTree as ET

root = ET.parse(sys.argv[1]).getroot()
ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
counters = root.find("t:ResultSummary/t:Counters", ns)
assert counters is not None, "Missing test counters"
for key, expected in {"total": "53", "executed": "53", "failed": "1", "passed": "52", "notExecuted": "0"}.items():
    assert counters.get(key) == expected, (key, counters.attrib)
results = root.findall("t:Results/t:UnitTestResult", ns)
failed = [test for test in results if test.get("outcome") == "Failed"]
assert len(results) == 53 and len(failed) == 1
assert failed[0].get("testName") == "AuthorizationExpiryTests.Abandoned_authorization_should_eventually_expire"
message = failed[0].findtext("t:Output/t:ErrorInfo/t:Message", namespaces=ns)
assert message and "Assert.Equal() Failure" in message and "Expected: 0" in message and "Actual:   1" in message, message
print("Verified 53 executed: 52 passed, one real expiry assertion failure (expected 0, actual 1), zero skips.")
