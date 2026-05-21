import sqlite3
import os

db_path = r"e:\Projects\Retail_Store\V\1.3.3\pos.db"
if not os.path.exists(db_path):
    print(f"Database not found at {db_path}")
    exit(1)

conn = sqlite3.connect(db_path)
cursor = conn.cursor()

try:
    cursor.execute("SELECT * FROM audit_logs ORDER BY created_at DESC LIMIT 20")
    rows = cursor.fetchall()
    for row in rows:
        print(row)
except Exception as e:
    print(f"Error: {e}")

conn.close()
