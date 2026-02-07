import sqlite3
conn = sqlite3.connect(r'D:/workspace/Shadowverse-beyond/Server/Data/shadowverse.db')
cur = conn.cursor()
cur.execute("PRAGMA table_info('CardPackDefinitions')")
print(cur.fetchall())
cur.execute("SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId")
print(cur.fetchall())
conn.close()
