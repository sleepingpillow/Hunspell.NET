#!/usr/bin/env python3
"""Verify the checked-in expectations against the pinned native Hunspell API."""

import argparse
import ctypes
import json
import os
from pathlib import Path
import tempfile


HERE = Path(__file__).resolve().parent
FIXTURES = HERE / "v1.7.6"


class NativeHunspell:
    def __init__(self, library):
        self.lib = ctypes.CDLL(str(library))
        self.lib.Hunspell_get_library_version.argtypes = []
        self.lib.Hunspell_get_library_version.restype = ctypes.c_char_p
        version = self.lib.Hunspell_get_library_version().decode("ascii")
        if version != "1.7.6":
            raise ValueError(f"Expected Hunspell 1.7.6, got {version}")

        self.lib.Hunspell_create.argtypes = [ctypes.c_char_p, ctypes.c_char_p]
        self.lib.Hunspell_create.restype = ctypes.c_void_p
        self.lib.Hunspell_destroy.argtypes = [ctypes.c_void_p]
        self.lib.Hunspell_destroy.restype = None
        self.lib.Hunspell_get_dic_encoding.argtypes = [ctypes.c_void_p]
        self.lib.Hunspell_get_dic_encoding.restype = ctypes.c_char_p
        self.lib.Hunspell_spell.argtypes = [ctypes.c_void_p, ctypes.c_char_p]
        self.lib.Hunspell_spell.restype = ctypes.c_int
        self.lib.Hunspell_add.argtypes = [ctypes.c_void_p, ctypes.c_char_p]
        self.lib.Hunspell_add.restype = ctypes.c_int
        self.lib.Hunspell_remove.argtypes = [ctypes.c_void_p, ctypes.c_char_p]
        self.lib.Hunspell_remove.restype = ctypes.c_int
        self.lib.Hunspell_suggest.argtypes = [
            ctypes.c_void_p, ctypes.POINTER(ctypes.POINTER(ctypes.c_char_p)), ctypes.c_char_p
        ]
        self.lib.Hunspell_suggest.restype = ctypes.c_int
        self.lib.Hunspell_free_list.argtypes = [
            ctypes.c_void_p, ctypes.POINTER(ctypes.POINTER(ctypes.c_char_p)), ctypes.c_int
        ]
        self.lib.Hunspell_free_list.restype = None

    def create(self, affix, dictionary):
        handle = self.lib.Hunspell_create(os.fsencode(affix), os.fsencode(dictionary))
        if not handle:
            raise RuntimeError(f"Unable to open {affix} and {dictionary}")
        return handle

    def encoding(self, handle):
        return self.lib.Hunspell_get_dic_encoding(handle).decode("ascii")

    def spell(self, handle, word):
        return bool(self.lib.Hunspell_spell(handle, word.encode(self.encoding(handle))))

    def suggest(self, handle, word):
        encoding = self.encoding(handle)
        result = ctypes.POINTER(ctypes.c_char_p)()
        count = self.lib.Hunspell_suggest(handle, ctypes.byref(result), word.encode(encoding))
        try:
            return [result[index].decode(encoding) for index in range(count)]
        finally:
            self.lib.Hunspell_free_list(handle, ctypes.byref(result), count)

    def remove(self, handle, word):
        if not self.spell(handle, word):
            raise AssertionError(f"Removal precondition failed: {word!r}")
        self.lib.Hunspell_remove(handle, word.encode(self.encoding(handle)))
        return self.spell(handle, word)

    def runtime(self, handle, steps):
        results = []
        for step in steps:
            op, word = step["Operation"], step["Word"]
            if op == "spell":
                results.append(self.spell(handle, word))
                continue
            if op == "remove":
                mutate = self.lib.Hunspell_remove
            elif op == "add":
                mutate = self.lib.Hunspell_add
            else:
                raise ValueError(f"Unknown runtime operation: {op}")
            if mutate(handle, word.encode(self.encoding(handle))) != 0:
                raise RuntimeError(f"Hunspell {op} failed: {word!r}")
        return results


def required_words(path):
    words = [line for line in path.read_text(encoding="utf-8").splitlines() if line.strip()]
    if not words:
        raise ValueError(f"No words in required fixture: {path}")
    return words


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", type=Path, required=True, help="Path to the Hunspell 1.7.6 shared library")
    args = parser.parse_args()
    native = NativeHunspell(args.library.resolve())
    failures = []
    checked = 0

    def compare(case_id, expected, actual):
        nonlocal checked
        checked += 1
        if expected != actual:
            failures.append(f"{case_id}: expected {expected!r}, native returned {actual!r}")

    for name, lists in [("breakdefault", [("good", True), ("wrong", False)]),
                        ("slash", [("good", True)])]:
        handle = native.create(FIXTURES / f"{name}.aff", FIXTURES / f"{name}.dic")
        try:
            for extension, expected in lists:
                for index, word in enumerate(required_words(FIXTURES / f"{name}.{extension}"), 1):
                    compare(f"upstream/{name}/{extension}/{index}", expected, native.spell(handle, word))
        finally:
            native.lib.Hunspell_destroy(handle)

    cases = json.loads((HERE / "synthetic-cases.json").read_text(encoding="utf-8"))
    if not cases:
        raise ValueError("No synthetic cases")
    for case in cases:
        with tempfile.TemporaryDirectory(prefix="hunspell-reference-") as directory:
            affix = Path(directory) / "test.aff"
            dictionary = Path(directory) / "test.dic"
            affix.write_bytes(case["Affix"].encode("utf-8"))
            dictionary.write_bytes(case["Dictionary"].encode("utf-8"))
            handle = native.create(affix, dictionary)
            try:
                operation = case["Operation"]
                if operation == "spell":
                    actual = native.spell(handle, case["Input"])
                elif operation == "suggest":
                    actual = native.suggest(handle, case["Input"])
                elif operation == "remove":
                    actual = native.remove(handle, case["Input"])
                elif operation == "runtime":
                    actual = native.runtime(handle, case["Steps"])
                else:
                    raise ValueError(f"Unknown reference operation: {operation}")
                compare(case["Id"], case["Expected"], actual)
            finally:
                native.lib.Hunspell_destroy(handle)

    print(f"Hunspell 1.7.6: checked {checked} cases; {len(failures)} expectation differences")
    for failure in failures:
        print(failure)
    return int(bool(failures))


if __name__ == "__main__":
    raise SystemExit(main())
