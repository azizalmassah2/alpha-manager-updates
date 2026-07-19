import sqlite3
db_path = r'd:\LUXCARD\desktop\user-manager\sqldb'
conn = sqlite3.connect(db_path)
try:
    cursor = conn.cursor()
    cursor.execute('SELECT id, userName, password, disabled, actualProfileName, downloadUsed, uploadUsed, uptimeUsed, regDate, lastSeenAt, lastIp FROM [user] ORDER BY id DESC LIMIT 10')
    print(cursor.fetchall())
except Exception as e:
    print(f'ERROR: {e}')
conn.close()
