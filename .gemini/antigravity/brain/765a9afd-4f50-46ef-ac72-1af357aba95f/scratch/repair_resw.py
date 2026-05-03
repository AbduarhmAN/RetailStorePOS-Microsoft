import xml.etree.ElementTree as ET
import os

resw_path = r'e:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Strings\en-US\Resources.resw'

# Define the full list of data elements for the dashboard section
dashboard_data = [
    ("ReportsDashboard_KPI_SalesToday.Text", "Sales Today"),
    ("ReportsDashboard_KPI_NetProfit.Text", "Net Profit Today"),
    ("ReportsDashboard_KPI_InvoiceCount.Text", "Invoice Count"),
    ("ReportsDashboard_KPI_AvgInvoiceValue.Text", "Avg. Invoice Value"),
    ("ReportsDashboard_MedianLabel.Text", "Median:"),
    ("ReportsDashboard_MedianStack.FlowDirection", "RightToLeft"),
    ("ReportsDashboard_Target_Title.Text", "Sales vs Target"),
    ("ReportsDashboard_Target_TodayLabel.Text", "TODAY"),
    ("ReportsDashboard_Target_GoalLabel.Text", "TARGET GOAL"),
    ("ReportsDashboard_Alerts_Title.Text", "Critical Alerts"),
    ("ReportsDashboard_TopProducts_Title.Text", "Top Selling Products"),
    ("ReportsDashboard_StockStatus_Title.Text", "Low Stock / Out of Stock"),
    ("ReportsDashboard_Discounts_Title.Text", "Discounts Today"),
]

# Register namespaces to preserve prefix if any
ET.register_namespace('', "http://schemas.microsoft.com/developer/msbuild/2003") # Actually resw uses default namespace

try:
    tree = ET.parse(resw_path)
    root = tree.getroot()

    # Find all 'data' elements
    data_elements = root.findall('data')
    
    # We want to insert our fixed elements before 'ReportsDashboard_StockHealth_Title.Text' 
    # and remove the broken ones.
    
    insert_index = -1
    for i, elem in enumerate(root):
        if elem.tag == 'data' and elem.attrib.get('name') == 'ReportsDashboard_StockHealth_Title.Text':
            insert_index = i
            break
            
    # Remove existing partial/broken dashboard elements to avoid duplicates
    dashboard_keys = [k for k, v in dashboard_data]
    to_remove = []
    for child in root:
        if child.tag == 'data' and child.attrib.get('name') in dashboard_keys:
            to_remove.append(child)
        if child.tag == 'data' and child.attrib.get('name') == 'ReportsDashboard_KPI_SalesToday.Text':
            if insert_index == -1: # Fallback
                 insert_index = list(root).index(child)
            to_remove.append(child)

    for elem in to_remove:
        root.remove(elem)

    # Re-calculate insert index if it was affected
    insert_index = -1
    for i, elem in enumerate(root):
        if elem.tag == 'data' and elem.attrib.get('name') == 'ReportsDashboard_StockHealth_Title.Text':
            insert_index = i
            break

    if insert_index == -1:
        insert_index = len(root)

    # Insert new elements
    for name, value in reversed(dashboard_data):
        new_data = ET.Element('data', {'name': name, 'xml:space': 'preserve'})
        val_elem = ET.SubElement(new_data, 'value')
        val_elem.text = value
        root.insert(insert_index, new_data)

    tree.write(resw_path, encoding='utf-8', xml_declaration=True)
    print("Successfully repaired Resources.resw")

except Exception as e:
    print(f"Error: {e}")
