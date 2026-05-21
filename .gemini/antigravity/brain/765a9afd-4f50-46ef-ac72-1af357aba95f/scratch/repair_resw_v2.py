import re

resw_path = r'e:\Projects\Retail_Store\V\1.3.3\Nexill.RetailStorePOS\Strings\en-US\Resources.resw'

with open(resw_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Define the block we want to ensure exists
dashboard_section = """  <!-- Reports Dashboard Page -->
  <data name="ReportsDashboard_StaleStatusText.Text" xml:space="preserve">
    <value>Viewing Snapshot (Refreshing...)</value>
  </data>
  <data name="ReportsDashboard_RefreshFailedSnapshot" xml:space="preserve">
    <value>Refresh Failed (Showing Snapshot)</value>
  </data>
  <data name="ReportsDashboard_Subtitle.Text" xml:space="preserve">
    <value>Here's an overview of your store activity.</value>
  </data>
  <data name="ReportsDashboard_KPI_SalesToday.Text" xml:space="preserve">
    <value>Sales Today</value>
  </data>
  <data name="ReportsDashboard_KPI_NetProfit.Text" xml:space="preserve">
    <value>Net Profit Today</value>
  </data>
  <data name="ReportsDashboard_KPI_InvoiceCount.Text" xml:space="preserve">
    <value>Invoice Count</value>
  </data>
  <data name="ReportsDashboard_KPI_AvgInvoiceValue.Text" xml:space="preserve">
    <value>Avg. Invoice Value</value>
  </data>
  <data name="ReportsDashboard_MedianLabel.Text" xml:space="preserve">
    <value>Median:</value>
  </data>
  <data name="ReportsDashboard_MedianStack.FlowDirection" xml:space="preserve">
    <value>RightToLeft</value>
  </data>
  <data name="ReportsDashboard_Target_Title.Text" xml:space="preserve">
    <value>Sales vs Target</value>
  </data>
  <data name="ReportsDashboard_Target_TodayLabel.Text" xml:space="preserve">
    <value>TODAY</value>
  </data>
  <data name="ReportsDashboard_Target_GoalLabel.Text" xml:space="preserve">
    <value>TARGET GOAL</value>
  </data>
  <data name="ReportsDashboard_Alerts_Title.Text" xml:space="preserve">
    <value>Critical Alerts</value>
  </data>
  <data name="ReportsDashboard_TopProducts_Title.Text" xml:space="preserve">
    <value>Top Selling Products</value>
  </data>
  <data name="ReportsDashboard_StockStatus_Title.Text" xml:space="preserve">
    <value>Low Stock / Out of Stock</value>
  </data>
  <data name="ReportsDashboard_Discounts_Title.Text" xml:space="preserve">
    <value>Discounts Today</value>
  </data>
  <data name="ReportsDashboard_StockHealth_Title.Text" xml:space="preserve">
    <value>Stock Health</value>
  </data>
  <data name="ReportsDashboard_StockHealth_HealthyFormat" xml:space="preserve">
    <value>{0}%  Healthy</value>
  </data>
"""

# Find the start and end of the corrupted section
# Start: "<!-- Reports Dashboard Page -->"
# End: "  <data name="ReportsDashboard_StockHealth_LowStockFormat" xml:space="preserve">"

pattern = re.compile(r'  <!-- Reports Dashboard Page -->.*?  <data name="ReportsDashboard_StockHealth_LowStockFormat"', re.DOTALL)

new_content = pattern.sub(dashboard_section + '  <data name="ReportsDashboard_StockHealth_LowStockFormat"', content)

with open(resw_path, 'w', encoding='utf-8') as f:
    f.write(new_content)

print("Successfully applied robust regex repair to Resources.resw")
