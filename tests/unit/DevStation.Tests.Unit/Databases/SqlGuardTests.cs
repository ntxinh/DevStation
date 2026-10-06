using DevStation.Application.Shared.Security;

namespace DevStation.Tests.Unit.Databases;

public class SqlGuardTests
{
    [Theory]
    // Read
    [InlineData("SELECT 1", SqlTier.Read)]
    [InlineData("select * from users", SqlTier.Read)]
    [InlineData("WITH x AS (SELECT 1) SELECT * FROM x", SqlTier.Read)]
    [InlineData("EXPLAIN SELECT * FROM t", SqlTier.Read)]
    [InlineData("EXPLAIN (ANALYZE, COSTS OFF) SELECT * FROM t", SqlTier.Read)]
    [InlineData("SHOW server_version", SqlTier.Read)]
    [InlineData("VALUES (1,2)", SqlTier.Read)]
    [InlineData("TABLE t", SqlTier.Read)]
    [InlineData("SELECT * FROM (SELECT 1) x", SqlTier.Read)]
    // Write
    [InlineData("INSERT INTO t VALUES (1)", SqlTier.Write)]
    [InlineData("UPDATE t SET a = 1", SqlTier.Write)]
    [InlineData("DELETE FROM t", SqlTier.Write)]
    [InlineData("MERGE INTO t USING s ON t.id = s.id WHEN MATCHED THEN UPDATE SET x = 1", SqlTier.Write)]
    [InlineData("CALL my_proc()", SqlTier.Write)]
    [InlineData("EXEC sp_help", SqlTier.Write)]
    [InlineData("EXECUTE my_stmt", SqlTier.Write)]
    [InlineData("EXPLAIN ANALYZE DELETE FROM t", SqlTier.Write)]
    [InlineData("EXPLAIN (ANALYZE) UPDATE t SET a = 1", SqlTier.Write)]
    [InlineData("WITH d AS (DELETE FROM t RETURNING *) SELECT * FROM d", SqlTier.Write)]
    // Dangerous
    [InlineData("DROP TABLE t", SqlTier.Dangerous)]
    [InlineData("TRUNCATE t", SqlTier.Dangerous)]
    [InlineData("ALTER TABLE t ADD c int", SqlTier.Dangerous)]
    [InlineData("CREATE TABLE t (a int)", SqlTier.Dangerous)]
    [InlineData("GRANT SELECT ON t TO u", SqlTier.Dangerous)]
    [InlineData("REVOKE ALL ON t FROM u", SqlTier.Dangerous)]
    [InlineData("DENY SELECT ON t TO u", SqlTier.Dangerous)]
    [InlineData("BACKUP DATABASE d TO DISK='x'", SqlTier.Dangerous)]
    [InlineData("RESTORE DATABASE d", SqlTier.Dangerous)]
    [InlineData("ATTACH DATABASE 'f' AS x", SqlTier.Dangerous)]
    [InlineData("DO $$ BEGIN RAISE NOTICE 'x'; END $$", SqlTier.Dangerous)]
    [InlineData("VACUUM FULL t", SqlTier.Dangerous)]
    [InlineData("SET search_path = public", SqlTier.Dangerous)]
    [InlineData("garbage not sql !!!", SqlTier.Dangerous)]
    [InlineData("", SqlTier.Dangerous)]
    [InlineData("   -- only a comment", SqlTier.Dangerous)]
    [InlineData(";;", SqlTier.Dangerous)]
    // comment / literal stripping
    [InlineData("-- DROP TABLE x\nSELECT 1", SqlTier.Read)]
    [InlineData("/* DROP TABLE x */ SELECT 1", SqlTier.Read)]
    [InlineData("/* /* nested */ still comment */ SELECT 1", SqlTier.Read)]
    [InlineData("SELECT '--DROP TABLE x'", SqlTier.Read)]
    [InlineData("SELECT 'it''s a string; with ; semis'", SqlTier.Read)]
    [InlineData("SELECT \"a;drop\" FROM t", SqlTier.Read)]
    [InlineData("SELECT $$DROP TABLE x$$", SqlTier.Read)]
    [InlineData("SELECT e'escape\\'; DROP TABLE x'", SqlTier.Read)]
    [InlineData("SELECT 1 /* unterminated comment hides rest", SqlTier.Read)]
    public void Classify_tiers(string sql, SqlTier expected)
        => Assert.Equal(expected, SqlGuard.Classify(sql).Tier);

    [Fact]
    public void Multi_statement_bumps_to_write_and_flags()
    {
        var v = SqlGuard.Classify("SELECT 1; SELECT 2");
        Assert.True(v.MultiStatement);
        Assert.Equal(SqlTier.Write, v.Tier);
    }

    [Theory]
    [InlineData("SELECT 1; DROP TABLE x", SqlTier.Dangerous)]
    [InlineData("INSERT INTO t VALUES (1); SELECT 2", SqlTier.Write)]
    [InlineData("-- comment\nSELECT 1;", SqlTier.Read)] // trailing ';' alone isn't multi
    public void Multi_statement_cases(string sql, SqlTier tier)
    {
        var v = SqlGuard.Classify(sql);
        Assert.Equal(tier, v.Tier);
        Assert.Equal(sql.TrimEnd(';', ' ', '\t', '\n').Contains(';'), v.MultiStatement);
    }

    [Theory]
    // smuggling attempts must not read as Read
    [InlineData("WITH x AS (DROP TABLE t) SELECT 1", SqlTier.Dangerous)]
    [InlineData("(UPDATE t SET a = 1)", SqlTier.Write)]
    [InlineData("SELEC\\T", SqlTier.Dangerous)]
    [InlineData("SELECT /* sneaky ; */ 1; DROP TABLE x -- tail", SqlTier.Dangerous)]
    public void Smuggling_fails_safe(string sql, SqlTier expected)
        => Assert.Equal(expected, SqlGuard.Classify(sql).Tier);

    [Fact]
    public void Null_and_whitespace_are_dangerous()
    {
        Assert.Equal(SqlTier.Dangerous, SqlGuard.Classify(null).Tier);
        Assert.Equal(SqlTier.Dangerous, SqlGuard.Classify("  \n\t ").Tier);
    }
}
