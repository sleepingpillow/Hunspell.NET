import os

def check_file(path):
    print(f"Checking {path}")
    with open(path, 'rb') as f:
        data = f.read()
        print(f"Bytes: {data}")
        try:
            print(f"UTF-8: {data.decode('utf-8')}")
        except:
            print("Not valid UTF-8")

        try:
            print(f"ISO-8859-2: {data.decode('iso-8859-2')}")
        except:
            print("Not valid ISO-8859-2")

base = r"c:\Users\Niklas\OneDrive\Dokument\GitHub\Hunspell.NET\tests\Hunspell.Tests\dictionaries\condition"
check_file(os.path.join(base, "condition.dic"))
check_file(os.path.join(base, "condition.good"))
check_file(os.path.join(base, "condition.aff"))
