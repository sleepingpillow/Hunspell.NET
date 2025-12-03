p='c:/Users/Niklas/OneDrive/Dokument/GitHub/Hunspell.NET/src/Hunspell/AffixManager.cs'
start_class=None
with open(p,'r',encoding='utf-8') as f:
    for i,line in enumerate(f, start=1):
        if 'internal sealed class AffixManager' in line:
            start_class=i
            break
print('class starts at', start_class)
# now find first time after start when balance==0
bal=0
first_zero_after_class=None
with open(p,'r',encoding='utf-8') as f:
    for i,line in enumerate(f, start=1):
        for c in line:
            if c=='{': bal+=1
            elif c=='}': bal-=1
        if i>=start_class and bal==0:
            first_zero_after_class=i
            break
print('first zero after class at', first_zero_after_class)
