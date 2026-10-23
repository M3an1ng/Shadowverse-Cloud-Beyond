import sqlite3
conn = sqlite3.connect(r"Data/shadowverse.db")
cur = conn.cursor()
cur.execute("SELECT name FROM sqlite_master WHERE type='table' ORDER BY name")
for (name,) in cur.fetchall():
    print(name)
conn.close()
