import xml.etree.ElementTree as ET
import os

def sync_resource(src_path, dest_path):
    # Parse source (en-US)
    ET.register_namespace('xsd', 'http://www.w3.org/2001/XMLSchema')
    ET.register_namespace('msdata', 'urn:schemas-microsoft-com:xml-msdata')
    
    src_tree = ET.parse(src_path)
    src_root = src_tree.getroot()
    
    # Extract all data nodes from source
    src_data = {node.get('name'): node for node in src_root.findall('data')}
    
    # Parse destination if it exists
    if os.path.exists(dest_path):
        dest_tree = ET.parse(dest_path)
        dest_root = dest_tree.getroot()
        dest_data = {node.get('name'): node for node in dest_root.findall('data')}
    else:
        # If not exists, copy source structure
        dest_root = ET.Element('root')
        # Copy headers and schema from source
        for child in src_root:
            if child.tag != 'data':
                dest_root.append(ET.fromstring(ET.tostring(child)))
        dest_data = {}

    # Update/Add keys from source to destination
    # We want to keep destination values if they already exist, otherwise use source (English)
    
    # Create a new root to preserve order and structure
    new_root = ET.Element('root')
    
    # Copy non-data elements first (schema, headers)
    for child in src_root:
        if child.tag != 'data':
            new_root.append(ET.fromstring(ET.tostring(child)))
    
    # Add data elements in source order
    for name, src_node in src_data.items():
        if name in dest_data:
            # Key exists in destination, keep the destination value
            new_root.append(dest_data[name])
        else:
            # Key missing in destination, add English value
            new_root.append(src_node)
            
    # Write back
    with open(dest_path, 'wb') as f:
        f.write(b'<?xml version="1.0" encoding="utf-8"?>\n')
        tree = ET.ElementTree(new_root)
        tree.write(f, encoding='utf-8', xml_declaration=False)

base_path = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'Strings'))
src = os.path.join(base_path, 'en-US', 'Resources.resw')
targets = ['ar-SA']

for lang in targets:
    dest = os.path.join(base_path, lang, 'Resources.resw')
    print(f"Syncing {lang}...")
    sync_resource(src, dest)
    print(f"Done.")
