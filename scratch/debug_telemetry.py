import pandas as pd
import os

data_dir = r"E:\Projects\Retail_Store\V\1.3.3\userAnalitices"
installations_csv = os.path.join(data_dir, "installations_rows.csv")

df_inst = pd.read_csv(installations_csv)
target_id = "80609db7-a1e8-4cb0-ba61-b0266f99d6bd"

user = df_inst[df_inst['install_id'] == target_id]
if not user.empty:
    print(f"FOUND USER: {target_id}")
    print(f"Country: '{user.iloc[0]['country_setting']}'")
    
    # Test filter
    is_eg = str(user.iloc[0]['country_setting']).upper() == 'EG'
    print(f"Is EG? {is_eg}")
else:
    print(f"USER NOT FOUND IN CSV: {target_id}")
