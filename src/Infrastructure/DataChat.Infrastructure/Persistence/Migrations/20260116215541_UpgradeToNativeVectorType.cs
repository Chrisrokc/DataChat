using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DataChat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeToNativeVectorType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration upgrades to SQL Server 2025 native VECTOR type if available.
            // If SQL Server 2025 is not available, it creates a compatible nvarchar column.
            // Uses dynamic SQL to avoid parse errors on older SQL Server versions.

            migrationBuilder.Sql(@"
                DECLARE @SupportsVector BIT = 0;

                -- Check if VECTOR type exists (SQL Server 2025+)
                IF EXISTS (SELECT 1 FROM sys.types WHERE name = 'vector')
                    SET @SupportsVector = 1;

                IF @SupportsVector = 1
                BEGIN
                    -- SQL Server 2025: Use native VECTOR type with dynamic SQL to avoid parse errors

                    -- Add new VECTOR column
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'EmbeddingVector')
                    BEGIN
                        EXEC sp_executesql N'ALTER TABLE DocumentChunks ADD EmbeddingVector VECTOR(1536) NULL';
                    END

                    -- Migrate existing data if old Embedding column exists and is nvarchar
                    IF EXISTS (SELECT 1 FROM sys.columns c
                               JOIN sys.types t ON c.user_type_id = t.user_type_id
                               WHERE c.object_id = OBJECT_ID('DocumentChunks')
                               AND c.name = 'Embedding'
                               AND t.name = 'nvarchar')
                    BEGIN
                        EXEC sp_executesql N'UPDATE DocumentChunks SET EmbeddingVector = TRY_CAST(Embedding AS VECTOR(1536)) WHERE Embedding IS NOT NULL AND Embedding != ''''';
                        ALTER TABLE DocumentChunks DROP COLUMN Embedding;
                    END

                    -- Rename to Embedding if needed
                    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'EmbeddingVector')
                    AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'Embedding')
                    BEGIN
                        EXEC sp_rename 'DocumentChunks.EmbeddingVector', 'Embedding', 'COLUMN';
                    END

                    -- Try to create DiskANN index if it doesn't exist (may fail on some SQL Server versions)
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentChunks_Embedding' AND object_id = OBJECT_ID('DocumentChunks'))
                    BEGIN
                        BEGIN TRY
                            EXEC sp_executesql N'CREATE VECTOR INDEX IX_DocumentChunks_Embedding ON DocumentChunks(Embedding) WITH (METRIC = ''cosine'', TYPE = DISKANN)';
                            PRINT 'Created DiskANN index for fast vector search.';
                        END TRY
                        BEGIN CATCH
                            PRINT 'DiskANN index creation failed (may not be supported). Vector search will still work but may be slower.';
                            PRINT ERROR_MESSAGE();
                        END CATCH
                    END

                    PRINT 'Upgraded to SQL Server 2025 native VECTOR type.';
                END
                ELSE
                BEGIN
                    -- Pre-SQL Server 2025: Keep using nvarchar for JSON storage
                    -- The SqlServerVectorStore has a fallback for in-memory cosine similarity

                    -- Ensure Embedding column exists
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DocumentChunks') AND name = 'Embedding')
                    BEGIN
                        ALTER TABLE DocumentChunks ADD Embedding NVARCHAR(MAX) NULL;
                    END

                    PRINT 'SQL Server 2025 VECTOR type not available. Using nvarchar fallback with in-memory search.';
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                -- Drop DiskANN index if it exists
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DocumentChunks_Embedding' AND object_id = OBJECT_ID('DocumentChunks'))
                BEGIN
                    DROP INDEX IX_DocumentChunks_Embedding ON DocumentChunks;
                END

                -- Check if current column is VECTOR type
                DECLARE @IsVectorType BIT = 0;
                IF EXISTS (SELECT 1 FROM sys.columns c
                           JOIN sys.types t ON c.user_type_id = t.user_type_id
                           WHERE c.object_id = OBJECT_ID('DocumentChunks')
                           AND c.name = 'Embedding'
                           AND t.name = 'vector')
                BEGIN
                    SET @IsVectorType = 1;
                END

                IF @IsVectorType = 1
                BEGIN
                    -- Convert VECTOR back to nvarchar
                    ALTER TABLE DocumentChunks ADD EmbeddingJson NVARCHAR(MAX) NULL;

                    EXEC sp_executesql N'UPDATE DocumentChunks SET EmbeddingJson = CAST(Embedding AS NVARCHAR(MAX)) WHERE Embedding IS NOT NULL';

                    ALTER TABLE DocumentChunks DROP COLUMN Embedding;

                    EXEC sp_rename 'DocumentChunks.EmbeddingJson', 'Embedding', 'COLUMN';
                END
                -- If it's already nvarchar, no changes needed
            ");
        }
    }
}
