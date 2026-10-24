SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.StageAttributionCorrections', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.StageAttributionCorrections
    (
        StageAttributionCorrectionId int IDENTITY(1,1) NOT NULL,
        PieceID int NOT NULL,
        TrackingEventID int NOT NULL,
        PreviousEmployeeId int NULL,
        PreviousEmployeeCode nvarchar(80) NULL,
        CorrectEmployeeId int NOT NULL,
        CorrectEmployeeCode nvarchar(80) NOT NULL,
        Stage nvarchar(120) NOT NULL,
        Reason nvarchar(500) NOT NULL,
        IdempotencyKey nvarchar(200) NOT NULL,
        AuthorizedByUserId int NOT NULL,
        Status nvarchar(50) NOT NULL CONSTRAINT DF_StageAttributionCorrections_Status DEFAULT N'Applied',
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_StageAttributionCorrections_CreatedAt DEFAULT SYSUTCDATETIME(),
        CompletedAt datetime2 NULL,
        CONSTRAINT PK_StageAttributionCorrections PRIMARY KEY (StageAttributionCorrectionId),
        CONSTRAINT FK_StageAttributionCorrections_Pieces FOREIGN KEY (PieceID) REFERENCES dbo.Pieces(PieceID),
        CONSTRAINT FK_StageAttributionCorrections_TrackingEvents FOREIGN KEY (TrackingEventID) REFERENCES dbo.TrackingEvents(TrackingEventID),
        CONSTRAINT FK_StageAttributionCorrections_Employees FOREIGN KEY (CorrectEmployeeId) REFERENCES dbo.Employees(EmployeeID),
        CONSTRAINT FK_StageAttributionCorrections_Users FOREIGN KEY (AuthorizedByUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT UQ_StageAttributionCorrections_Idempotency UNIQUE (IdempotencyKey)
    );

    CREATE INDEX IX_StageAttributionCorrections_PieceId ON dbo.StageAttributionCorrections(PieceID);
    CREATE INDEX IX_StageAttributionCorrections_TrackingEventId ON dbo.StageAttributionCorrections(TrackingEventID);
    CREATE INDEX IX_StageAttributionCorrections_IdempotencyKey ON dbo.StageAttributionCorrections(IdempotencyKey);
END;

COMMIT TRANSACTION;
