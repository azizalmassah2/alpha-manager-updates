import sqlite3
import os

db_path = r"C:\Users\MrAziz\AppData\Local\Lux Platform\platform.db"

if not os.path.exists(db_path):
    print("Database not found!")
    exit(1)

conn = sqlite3.connect(db_path)
cursor = conn.cursor()

# Get column names and some rows of Vouchers
cursor.execute("PRAGMA table_info(Vouchers);")
cols = [col[1] for col in cursor.fetchall()]
print("Vouchers columns:", cols)

cursor.execute("SELECT * FROM Vouchers LIMIT 5;")
rows = cursor.fetchall()
print(f"Total rows found: {len(rows)}")
for r in rows:
    print(dict(zip(cols, r)))

conn.close()
