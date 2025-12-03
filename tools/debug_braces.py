p=r'c:/Users/Niklas/OneDrive/Dokument/GitHub/Hunspell.NET/src/Hunspell/AffixManager.cs'
with open(p,'r',encoding='utf-8') as f:
    bal=0
    last_zero = None
    for i,line in enumerate(f, start=1):
        for c in line:
            if c=='{': bal+=1
            elif c=='}': bal-=1
        if bal==0:
            last_zero = i
        if bal<0:
            print('Balance negative at line', i)
            break
    print('Last line where balance==0 before negative:', last_zero)
