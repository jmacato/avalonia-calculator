import sys
import xml.etree.ElementTree as ET
import os
import re

def escape_cpp_string(s):
    """Escape a string for C++ string literal."""
    return re.sub(r'([\\"\n])', r'\\\1', s).replace('\r', '\\r')

def convert_resw_to_header(output_file, input_files):
    print(f"Output file: {output_file}")
    print(f"Input files: {input_files}")
    print(f"Current working directory: {os.getcwd()}")

    combined_resources = {}

    for input_file in input_files:
        if not os.path.exists(input_file):
            print(f"Error: Input file does not exist: {input_file}")
            sys.exit(1)

        tree = ET.parse(input_file)
        root = tree.getroot()

        for data in root.findall(".//data"):
            name = data.get('name')
            value = data.find('value').text
            if value is not None:
                combined_resources[name] = value
            else:
                print(f"Warning: Empty value for key '{name}' in file {input_file}")

    with open(output_file, 'w', encoding='utf-8') as f:
        f.write("#pragma once\n\n")
        f.write("#include <string>\n")
        f.write("#include <unordered_map>\n\n")
        f.write("namespace Resources {\n\n")
        f.write("const std::unordered_map<std::wstring, std::wstring> StringResources = {\n")

        for name, value in combined_resources.items():
            escaped_name = escape_cpp_string(name)
            escaped_value = escape_cpp_string(value)
            f.write(f'    {{L"{escaped_name}", L"{escaped_value}"}},\n')

        f.write("};\n\n")
        f.write("} // namespace Resources\n")

if __name__ == "__main__":
    if len(sys.argv) < 3:
        print("Usage: python resw_to_header.py output.h input1.resw [input2.resw ...]")
        sys.exit(1)

    output_file = sys.argv[1]
    input_files = sys.argv[2:]
    convert_resw_to_header(output_file, input_files)