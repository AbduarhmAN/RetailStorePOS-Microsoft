import re

resw_path = r'e:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Strings\en-US\Resources.resw'

with open(resw_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Remove the FlowDirection override from the resource file
pattern = re.compile(r'  <data name="ReportsDashboard_MedianStack\.FlowDirection" xml:space="preserve">.*?</data>\n', re.DOTALL)

new_content = pattern.sub('', content)

with open(resw_path, 'w', encoding='utf-8') as f:
    f.write(new_content)

print("Successfully removed MedianStack.FlowDirection from en-US Resources.resw")
