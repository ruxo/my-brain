CREATE CONSTRAINT IF NOT EXISTS FOR (t:Tag) REQUIRE (t.name) IS UNIQUE;
CREATE CONSTRAINT IF NOT EXISTS FOR (lo:LinkObject) REQUIRE (lo.uri) IS UNIQUE;
CREATE INDEX IF NOT EXISTS FOR (c:Concept) ON (c.name);
CREATE INDEX IF NOT EXISTS FOR (c:Bookmark) ON (c.label);
CREATE FULLTEXT INDEX conceptNameIndex IF NOT EXISTS FOR (n:Concept) ON EACH [n.name]
CREATE FULLTEXT INDEX linkObjectNameIndex IF NOT EXISTS FOR (n:LinkObject) ON EACH [n.name]

CREATE (brain:Concept { name: 'Brain' })
CREATE (home:Bookmark { label: 'home' })-[:POINT]->(brain)