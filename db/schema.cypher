CREATE CONSTRAINT IF NOT EXISTS FOR (t:Tag) REQUIRE (t.name) IS UNIQUE;
CREATE CONSTRAINT IF NOT EXISTS FOR (lo:LinkObject) REQUIRE (lo.uri) IS UNIQUE;
CREATE INDEX IF NOT EXISTS FOR (c:Concept) ON (c.name);
CREATE INDEX IF NOT EXISTS FOR (c:Bookmark) ON (c.label);

CREATE (brain:Concept { name: 'Brain' })
CREATE (home:Bookmark { label: 'home' })-[:POINT]->(brain)