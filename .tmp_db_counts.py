import sqlite3
from pathlib import Path

db = r"D:/workspace/Shadowverse-beyond/Server/Data/shadowverse.db"
conn = sqlite3.connect(db)
cur = conn.cursor()
for table in ["ReplayEvents", "ReplayBundles", "ReplaySnapshots", "ReplayGames"]:
    try:
        cur.execute(f"SELECT COUNT(*) FROM {table}")
        count = cur.fetchone()[0]
        print(f"{table}: {count}")
    except Exception as e:
        print(f"{table}: error {e}")
conn.close()
