import re,sys
p=r'c:/Users/Niklas/OneDrive/Dokument/GitHub/Hunspell.NET/src/Hunspell/AffixManager.cs'
method_sig = 'private bool IsValidCompoundPart(string part, int wordCount, int startPos, int endPos, string fullWord, out bool requiresForceUCase)'
start=None
with open(p,'r',encoding='utf-8') as f:
    lines = f.readlines()
for i,line in enumerate(lines):
    if method_sig in line:
        start=i+1
        break
if start is None:
    print('Method signature not found')
    sys.exit(1)
# find first '{' after start
bal=0
first_open=None
for j in range(start, len(lines)):
    if '{' in lines[j]:
        first_open=j+1
        break
if first_open is None:
    print('No opening brace found for method')
    sys.exit(1)
# compute balance from first_open
bal=0
end=None
for k in range(first_open-1, len(lines)):
    line = lines[k]
    for ch in line:
        if ch=='{': bal+=1
        elif ch=='}': bal-=1
    if bal==0:
        end=k+1
        break
print(f'Method signature at line {start}, first brace at {first_open}, closing at {end}, final balance {bal}')

# print excerpt around first_open and end
for idx in range(first_open-5, first_open+5):
    if 0<=idx<len(lines):
        print(f'{idx+1:5}: {lines[idx].rstrip()}')
print('...')
for idx in range(end-6, end+1):
    if 0<=idx<len(lines):
        print(f'{idx+1:5}: {lines[idx].rstrip()}')
