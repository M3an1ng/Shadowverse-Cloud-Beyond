import sqlite3
conn = sqlite3.connect(r"Server/Data/shadowverse.db")
cur = conn.cursor()
cur.execute("select Region, Sequence, count(*), group_concat(Name, '|') from CardSetDefinitions group by Region, Sequence having count(*)>1")
print('dupes:', cur.fetchall())
cur.execute("select Id, Name, Region, Sequence from CardSetDefinitions order by Region, Sequence, Id")
rows = cur.fetchall()
print('rows:', rows[:50])
conn.close()
