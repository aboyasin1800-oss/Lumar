SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.FabricRollLabelPrintHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FabricRollLabelPrintHistory
    (
        FabricRollLabelPrintHistoryId bigint IDENTITY(1,1) NOT NULL,
        FabricRollId bigint NOT NULL,
        PrintedAtUtc datetime2(7) NOT NULL CONSTRAINT DF_FabricRollLabelPrintHistory_PrintedAtUtc DEFAULT SYSUTCDATETIME(),
        PrintOperationId uniqueidentifier NOT NULL,
        PrintedByUserId int NULL,
        CONSTRAINT PK_FabricRollLabelPrintHistory PRIMARY KEY (FabricRollLabelPrintHistoryId),
        CONSTRAINT FK_FabricRollLabelPrintHistory_FabricRoll FOREIGN KEY (FabricRollId)
            REFERENCES dbo.FabricRolls(FabricRollId) ON DELETE CASCADE,
        CONSTRAINT FK_FabricRollLabelPrintHistory_User FOREIGN KEY (PrintedByUserId)
            REFERENCES dbo.Users(UserID),
        CONSTRAINT UQ_FabricRollLabelPrintHistory_Operation UNIQUE (FabricRollId, PrintOperationId)
    );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FabricRollLabelPrintHistory')
      AND name = N'IX_FabricRollLabelPrintHistory_FabricRollId_PrintedAtUtc'
)
BEGIN
    CREATE INDEX IX_FabricRollLabelPrintHistory_FabricRollId_PrintedAtUtc
        ON dbo.FabricRollLabelPrintHistory(FabricRollId, PrintedAtUtc DESC);
END;

COMMIT TRANSACTION;
