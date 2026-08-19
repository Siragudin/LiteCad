# -*- coding: utf-8 -*-
p = r"e:\Games\Sid Meier's Civilization 4 Complete\Beyond the Sword\Assets\Python\CvDiplomacy.py"
with open(p, "rb") as f:
    data = f.read()

start = data.find(b"    def setAIComment")
end = data.find(b"\tdef performHeadAction")
print("start", start, "end", end)
print("CRLF", b"\r\n" in data)
print("snippet around start:")
print(repr(data[start:start+80]))
print("snippet around end:")
print(repr(data[end-20:end+40]))
