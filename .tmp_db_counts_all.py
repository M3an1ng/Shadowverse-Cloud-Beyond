import sqlite3
from pathlib import Path

db = r"D:/workspace/Shadowverse-beyond/Server/Data/shadowverse.db"
conn = sqlite3.connect(db)
cur = conn.cursor()
cur.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")
tables = [r[0] for r in cur.fetchall()]
counts = []
for table in tables:
    try:
        cur.execute(f"SELECT COUNT(*) FROM {table}")
        count = cur.fetchone()[0]
        counts.append((table, count))
    except Exception:
        pass
counts.sort(key=lambda x: x[1], reverse=True)
for table, count in counts[:20]:
    print(f"{table}: {count}")
conn.close()
