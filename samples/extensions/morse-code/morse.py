"""Monitoring Tools extension: reads one JSON line, writes one JSON line."""
import json
import sys

CODE = {".-": "A", "-...": "B", "-.-.": "C", "-..": "D", ".": "E", "..-.": "F", "--.": "G", "....": "H", "..": "I",
        ".---": "J", "-.-": "K", ".-..": "L", "--": "M", "-.": "N", "---": "O", ".--.": "P", "--.-": "Q", ".-.": "R",
        "...": "S", "-": "T", "..-": "U", "...-": "V", ".--": "W", "-..-": "X", "-.--": "Y", "--..": "Z",
        "-----": "0", ".----": "1", "..---": "2", "...--": "3", "....-": "4", ".....": "5", "-....": "6",
        "--...": "7", "---..": "8", "----.": "9"}

request = json.loads(sys.stdin.readline())
words = request["text"].strip().split("/")
decoded = " ".join("".join(CODE.get(letter, "?") for letter in word.split()) for word in words)
print(json.dumps({"copy": decoded, "values": [{"label": "Decoded", "text": decoded}], "toast": "Decoded: " + decoded}))
