import os

base = r"c:\Users\Niklas\OneDrive\Dokument\GitHub\Hunspell.NET\tests\Hunspell.Tests\dictionaries\condition"
aff_path = os.path.join(base, "condition.aff")
dic_path = os.path.join(base, "condition.dic")

# Reconstruct condition.dic
dic_content = """5
ofo/SP
entertain/Q
nianretne/R
éra/Z
wry/TU
"""
with open(dic_path, 'w', encoding='iso-8859-2') as f:
    f.write(dic_content)

print(f"Fixed {dic_path}")

# Reconstruct condition.aff
# I'll read the file as binary, find the corrupted lines, and replace them.
# Or just rewrite the whole file if I know the content.
# The file is small. I can read it as UTF-8 (with replacement chars), fix the strings, and save as ISO-8859-2.

with open(aff_path, 'r', encoding='utf-8', errors='replace') as f:
    lines = f.readlines()

new_lines = []
for line in lines:
    if "SFX Z" in line and "[" in line:
        # Replace the bracketed part
        # The corrupted part is likely []
        # I'll replace it with [aeioué]
        # But I need to be careful about the regex.
        import re
        new_line = re.sub(r"\[.*?\]", "[aeioué]", line)
        new_lines.append(new_line)
    else:
        new_lines.append(line)

with open(aff_path, 'w', encoding='iso-8859-2') as f:
    f.writelines(new_lines)

print(f"Fixed {aff_path}")

# Fix condition.good
# It is currently UTF-8, but should be ISO-8859-2 to match .aff
good_path = os.path.join(base, "condition.good")
with open(good_path, 'r', encoding='utf-8') as f:
    content = f.read()

with open(good_path, 'w', encoding='iso-8859-2') as f:
    f.write(content)

print(f"Fixed {good_path}")
