import sqlite3
import os

db_path = r"C:\Users\MrAziz\AppData\Local\LuxCard\Routers\HGN09XXPA98.db"

if not os.path.exists(db_path):
    print("Database not found!")
    exit(1)

conn = sqlite3.connect(db_path)
cursor = conn.cursor()

# List all tables
cursor.execute("SELECT name FROM sqlite_master WHERE type='table';")
tables = [r[0] for r in cursor.fetchall()]
print("Tables in router DB:", tables)

for table in tables:
    if "Template" in table:
        print(f"\n--- Content of {table} ---")
        try:
            cursor.execute(f"PRAGMA table_info({table});")
            cols = [col[1] for col in cursor.fetchall()]
            print("Columns:", cols)
            
            cursor.execute(f"SELECT * FROM {table};")
            rows = cursor.fetchall()
            print(f"Total rows: {len(rows)}")
            for row in rows:
                row_dict = dict(zip(cols, row))
                # print name and paths
                print({k: row_dict.get(k) for k in ['Id', 'Name', 'BackgroundImagePath', 'LogoImagePath', 'Columns', 'Rows']})
        except Exception as e:
            print("Error:", e)

conn.close()
